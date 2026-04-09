using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using NeoCoffeeServer.Contracts;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NeoCoffeeApi.Controllers
{
    [ApiController]
    [Route("api/orders")]
    public class OrdersController : ControllerBase
    {
        private readonly string _connectionString = @"Data Source=F:\Учеба\Хакатон\Сервер\NeoCoffeeServer\NeoCoffeeServer\coffee.db";

        [HttpPost]
        public IActionResult CreateOrder([FromBody] CreateOrderRequest request)
        {
            if (request == null || request.Items == null || request.Items.Count == 0)
                return BadRequest("Пустой заказ.");

            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();

                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        long orderId;

                        // 1. Insert Orders
                        using (var orderCmd = connection.CreateCommand())
                        {
                            orderCmd.Transaction = transaction;
                            orderCmd.CommandText = @"
INSERT INTO Orders (Number, CreatedAt, Status, TotalAmount, CreatedByUserId)
VALUES ($number, $createdAt, $status, $totalAmount, $createdByUserId);
SELECT last_insert_rowid();
";
                            orderCmd.Parameters.AddWithValue("$number", request.Number);
                            orderCmd.Parameters.AddWithValue("$createdAt", DateTime.UtcNow.ToString("o"));
                            orderCmd.Parameters.AddWithValue("$status", request.Status ?? "Paid");
                            orderCmd.Parameters.AddWithValue("$totalAmount", request.TotalAmount);
                            orderCmd.Parameters.AddWithValue("$createdByUserId", request.CreatedByUserId);

                            orderId = (long)orderCmd.ExecuteScalar();
                        }

                        // 2. Insert OrderItems
                        foreach (var item in request.Items)
                        {
                            using (var itemCmd = connection.CreateCommand())
                            {
                                itemCmd.Transaction = transaction;
                                itemCmd.CommandText = @"
INSERT INTO OrderItems (OrderId, ProductId, ProductNameSnapshot, UnitPriceSnapshot, Quantity, Notes)
VALUES ($orderId, $productId, $productNameSnapshot, $unitPriceSnapshot, $quantity, $notes);
";
                                itemCmd.Parameters.AddWithValue("$orderId", orderId);
                                itemCmd.Parameters.AddWithValue("$productId", item.ProductId);
                                itemCmd.Parameters.AddWithValue("$productNameSnapshot", item.ProductNameSnapshot ?? "");
                                itemCmd.Parameters.AddWithValue("$unitPriceSnapshot", item.UnitPriceSnapshot);
                                itemCmd.Parameters.AddWithValue("$quantity", item.Quantity);
                                itemCmd.Parameters.AddWithValue("$notes", item.Notes ?? "");

                                itemCmd.ExecuteNonQuery();
                            }
                        }

                        transaction.Commit();

                        return Ok(new
                        {
                            OrderId = orderId,
                            Number = request.Number,
                            Status = "Paid"
                        });
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        return StatusCode(500, "Ошибка при сохранении заказа: " + ex.Message);
                    }
                }
            }
        }

        [HttpGet("pickup")]
        public IActionResult GetPickupOrders()
        {
            var orders = new List<PickupOrderDto>();

            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();

                // 1. Заказы
                using (var orderCmd = connection.CreateCommand())
                {
                    orderCmd.CommandText = @"
SELECT 
    o.Id,
    o.Number,
    o.Status,
    o.TotalAmount,
    o.CreatedAt
FROM Orders o
WHERE o.Status IN ('Prepared', 'ReadyForPickup', 'Completed', 'Redo')
ORDER BY 
    CASE o.Status
        WHEN 'Prepared' THEN 0
        WHEN 'ReadyForPickup' THEN 1
        WHEN 'Redo' THEN 2
        WHEN 'Completed' THEN 3
        ELSE 99
    END,
    datetime(o.CreatedAt) DESC;
";

                    using (var reader = orderCmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            orders.Add(new PickupOrderDto
                            {
                                Id = reader.GetInt32(0),
                                Number = reader.IsDBNull(1) ? "" : reader.GetString(1),
                                Status = reader.IsDBNull(2) ? "" : reader.GetString(2),
                                TotalAmount = reader.IsDBNull(3) ? 0m : Convert.ToDecimal(reader.GetValue(3)),
                                CreatedAt = reader.IsDBNull(4) ? "" : reader.GetString(4),
                                Items = new List<PickupOrderItemDto>()
                            });
                        }
                    }
                }

                // 2. Товары заказа
                foreach (var order in orders)
                {
                    using (var itemCmd = connection.CreateCommand())
                    {
                        itemCmd.CommandText = @"
SELECT
    Id,
    ProductId,
    ProductNameSnapshot,
    UnitPriceSnapshot,
    Quantity,
    Notes
FROM OrderItems
WHERE OrderId = $orderId
ORDER BY Id;
";
                        itemCmd.Parameters.AddWithValue("$orderId", order.Id);

                        using (var itemReader = itemCmd.ExecuteReader())
                        {
                            while (itemReader.Read())
                            {
                                order.Items.Add(new PickupOrderItemDto
                                {
                                    Id = itemReader.GetInt32(0),
                                    ProductId = itemReader.IsDBNull(1) ? 0 : itemReader.GetInt32(1),
                                    ProductNameSnapshot = itemReader.IsDBNull(2) ? "" : itemReader.GetString(2),
                                    UnitPriceSnapshot = itemReader.IsDBNull(3) ? 0m : Convert.ToDecimal(itemReader.GetValue(3)),
                                    Quantity = itemReader.IsDBNull(4) ? 1 : itemReader.GetInt32(4),
                                    Notes = itemReader.IsDBNull(5) ? "" : itemReader.GetString(5),
                                    Ingredients = new List<PickupIngredientDto>()
                                });
                            }
                        }
                    }

                    // 3. Ингредиенты для каждого товара
                    foreach (var item in order.Items)
                    {
                        // 3.1 Сначала пробуем снимок заказа
                        using (var ingCmd = connection.CreateCommand())
                        {
                            ingCmd.CommandText = @"
SELECT
    Id,
    IngredientId,
    IngredientNameSnapshot,
    UnitSnapshot,
    FinalQuantity,
    IsRemoved
FROM OrderItemIngredients
WHERE OrderItemId = $orderItemId
ORDER BY Id;
";
                            ingCmd.Parameters.AddWithValue("$orderItemId", item.Id);

                            using (var ingReader = ingCmd.ExecuteReader())
                            {
                                while (ingReader.Read())
                                {
                                    item.Ingredients.Add(new PickupIngredientDto
                                    {
                                        Id = ingReader.GetInt32(0),
                                        IngredientId = ingReader.IsDBNull(1) ? 0 : ingReader.GetInt32(1),
                                        IngredientNameSnapshot = ingReader.IsDBNull(2) ? "" : ingReader.GetString(2),
                                        UnitSnapshot = ingReader.IsDBNull(3) ? "" : ingReader.GetString(3),
                                        FinalQuantity = ingReader.IsDBNull(4) ? 0m : Convert.ToDecimal(ingReader.GetValue(4)),
                                        IsRemoved = !ingReader.IsDBNull(5) && ingReader.GetInt32(5) == 1
                                    });
                                }
                            }
                        }

                        // 3.2 Fallback: если снимка нет, грузим из ProductRecipes + Ingredients
                        if (item.Ingredients.Count == 0)
                        {
                            using (var recipeCmd = connection.CreateCommand())
                            {
                                recipeCmd.CommandText = @"
SELECT
    pr.IngredientId,
    i.Name,
    i.Unit,
    pr.Quantity,
    pr.IsOptional
FROM ProductRecipes pr
INNER JOIN Ingredients i ON i.Id = pr.IngredientId
WHERE pr.ProductId = $productId
ORDER BY pr.SortOrder, pr.Id;
";
                                recipeCmd.Parameters.AddWithValue("$productId", item.ProductId);

                                using (var recipeReader = recipeCmd.ExecuteReader())
                                {
                                    while (recipeReader.Read())
                                    {
                                        item.Ingredients.Add(new PickupIngredientDto
                                        {
                                            Id = 0,
                                            IngredientId = recipeReader.IsDBNull(0) ? 0 : recipeReader.GetInt32(0),
                                            IngredientNameSnapshot = recipeReader.IsDBNull(1) ? "" : recipeReader.GetString(1),
                                            UnitSnapshot = recipeReader.IsDBNull(2) ? "" : recipeReader.GetString(2),
                                            FinalQuantity = recipeReader.IsDBNull(3) ? 0m : Convert.ToDecimal(recipeReader.GetValue(3)),
                                            IsRemoved = !recipeReader.IsDBNull(4) && recipeReader.GetInt32(4) == 1
                                        });
                                    }
                                }
                            }
                        }
                    }
                }
            }

            return Ok(orders);
        }

        [HttpGet("board")]
        public IActionResult GetBoardOrders()
        {
            var orders = new List<BoardOrderDto>();

            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();

                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = @"
SELECT 
    o.Id,
    o.Number,
    o.Status
FROM Orders o
WHERE o.Status IN ('Paid', 'Accepted', 'Prepared', 'ReadyForPickup')
ORDER BY 
    CASE o.Status
        WHEN 'Paid' THEN 0
        WHEN 'Accepted' THEN 1
        WHEN 'Prepared' THEN 2
        WHEN 'ReadyForPickup' THEN 3
        ELSE 99
    END,
    datetime(o.CreatedAt) ASC;
";

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            orders.Add(new BoardOrderDto
                            {
                                Id = reader.GetInt32(0),
                                Number = reader.IsDBNull(1) ? "" : reader.GetString(1),
                                Status = reader.IsDBNull(2) ? "" : reader.GetString(2)
                            });
                        }
                    }
                }
            }

            return Ok(orders);
        }

        [HttpGet("barista")]
        public IActionResult GetBaristaOrders()
        {
            var orders = new List<BaristaOrderDto>();

            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();

                // 1) Сначала читаем заказы
                using (var orderCmd = connection.CreateCommand())
                {
                    orderCmd.CommandText = @"
SELECT 
    o.Id,
    o.Number,
    o.Status,
    o.TotalAmount,
    o.CreatedAt
FROM Orders o
WHERE o.Status IN ('Paid', 'Accepted', 'Prepared', 'ReadyForPickup', 'Completed', 'Redo')
ORDER BY 
    CASE o.Status
        WHEN 'Paid' THEN 0
        WHEN 'Accepted' THEN 1
        WHEN 'Prepared' THEN 2
        WHEN 'ReadyForPickup' THEN 3
        WHEN 'Completed' THEN 4
        ELSE 99
    END,
    datetime(o.CreatedAt) DESC;
";

                    using (var reader = orderCmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            orders.Add(new BaristaOrderDto
                            {
                                Id = reader.GetInt32(0),
                                Number = reader.IsDBNull(1) ? "" : reader.GetString(1),
                                Status = reader.IsDBNull(2) ? "" : reader.GetString(2),
                                TotalAmount = reader.IsDBNull(3) ? 0 : reader.GetDecimal(3),
                                CreatedAt = reader.IsDBNull(4) ? "" : reader.GetString(4),
                                Items = new List<BaristaOrderItemDto>()
                            });
                        }
                    }
                }

                // 2) Для каждого заказа читаем состав
                foreach (var order in orders)
                {
                    using (var itemCmd = connection.CreateCommand())
                    {
                        itemCmd.CommandText = @"
SELECT 
    ProductNameSnapshot,
    Quantity,
    Notes
FROM OrderItems
WHERE OrderId = $orderId
ORDER BY Id;
";
                        itemCmd.Parameters.AddWithValue("$orderId", order.Id);

                        using (var itemReader = itemCmd.ExecuteReader())
                        {
                            while (itemReader.Read())
                            {
                                order.Items.Add(new BaristaOrderItemDto
                                {
                                    ProductName = itemReader.IsDBNull(0) ? "" : itemReader.GetString(0),
                                    Quantity = itemReader.IsDBNull(1) ? 0 : itemReader.GetInt32(1),
                                    Notes = itemReader.IsDBNull(2) ? "" : itemReader.GetString(2)
                                });
                            }
                        }
                    }
                }
            }

            return Ok(orders);
        }

        [HttpPost("{id}/status")]
        public IActionResult UpdateOrderStatus(int id, [FromBody] UpdateOrderStatusRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Status))
                return BadRequest("Не указан новый статус.");

            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();

                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = @"
UPDATE Orders
SET Status = $status
WHERE Id = $id;
";
                    cmd.Parameters.AddWithValue("$status", request.Status);
                    cmd.Parameters.AddWithValue("$id", id);

                    var affected = cmd.ExecuteNonQuery();

                    if (affected == 0)
                        return NotFound("Заказ не найден.");
                }
            }

            return Ok(new
            {
                OrderId = id,
                Status = request.Status
            });
        }
    }

    public class CreateOrderRequest
    {
        public string Number { get; set; }
        public string Status { get; set; }
        public decimal TotalAmount { get; set; }
        public int CreatedByUserId { get; set; }
        public List<CreateOrderItemRequest> Items { get; set; }
    }

    public class CreateOrderItemRequest
    {
        public int ProductId { get; set; }
        public string ProductNameSnapshot { get; set; }
        public decimal UnitPriceSnapshot { get; set; }
        public int Quantity { get; set; }
        public string Notes { get; set; }
    }

    public class UpdateOrderStatusRequest
    {
        public string Status { get; set; }
    }

    public class BaristaOrderDto
    {
        public int Id { get; set; }
        public string Number { get; set; }
        public string Status { get; set; }
        public decimal TotalAmount { get; set; }
        public string CreatedAt { get; set; }
        public List<BaristaOrderItemDto> Items { get; set; }
    }

    public class BaristaOrderItemDto
    {
        public string ProductName { get; set; }
        public int Quantity { get; set; }
        public string Notes { get; set; }
    }

    public class PickupOrderDto
    {
        public int Id { get; set; }
        public string Number { get; set; }
        public string Status { get; set; }
        public decimal TotalAmount { get; set; }
        public string CreatedAt { get; set; }
        public List<PickupOrderItemDto> Items { get; set; }
    }

    public class PickupOrderItemDto
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public string ProductNameSnapshot { get; set; }
        public decimal UnitPriceSnapshot { get; set; }
        public int Quantity { get; set; }
        public string Notes { get; set; }
        public List<PickupIngredientDto> Ingredients { get; set; }
    }

    public class PickupIngredientDto
    {
        public int Id { get; set; }
        public int IngredientId { get; set; }
        public string IngredientNameSnapshot { get; set; }
        public string UnitSnapshot { get; set; }
        public decimal FinalQuantity { get; set; }
        public bool IsRemoved { get; set; }
    }

    public class BoardOrderDto
    {
        public int Id { get; set; }
        public string Number { get; set; }
        public string Status { get; set; }
    }
}