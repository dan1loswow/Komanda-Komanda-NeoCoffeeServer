using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.AspNetCore.Hosting;
using System.IO;
using System.Collections.Generic;

namespace NeoCoffeeApi.Controllers
{
    [ApiController]
    [Route("api/menu")]
    public class MenuController : ControllerBase
    {
        private readonly string _connectionString = @"Data Source=F:\Учеба\Хакатон\Сервер\NeoCoffeeServer\NeoCoffeeServer\coffee.db";

        [HttpGet("categories")]
        public IActionResult GetCategories()
        {
            var result = new List<string>();

            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();

                var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT DISTINCT Category
                    FROM Products
                    WHERE IsActive = 1
                    ORDER BY DisplayOrder, Category;
                ";

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        result.Add(reader.GetString(0));
                    }
                }
            }

            return Ok(result);
        }

        [HttpGet("products")]
        public IActionResult GetProducts([FromQuery] string category)
        {
            var result = new List<ProductMenuDto>();

            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();

                var command = connection.CreateCommand();

                if (string.IsNullOrWhiteSpace(category) || category == "Популярное")
                {
                    command.CommandText = @"
                        SELECT Id, Name, Description, Category, Price, ImagePath
                        FROM Products
                        WHERE IsActive = 1
                        ORDER BY DisplayOrder, Name;
                    ";
                }
                else
                {
                    command.CommandText = @"
                        SELECT Id, Name, Description, Category, Price, ImagePath
                        FROM Products
                        WHERE IsActive = 1 AND Category = $category
                        ORDER BY DisplayOrder, Name;
                    ";
                    command.Parameters.AddWithValue("$category", category);
                }

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        result.Add(new ProductMenuDto
                        {
                            Id = reader.GetInt32(0),
                            Name = reader.GetString(1),
                            Description = reader.IsDBNull(2) ? "" : reader.GetString(2),
                            Category = reader.GetString(3),
                            Price = reader.GetDecimal(4),
                            ImagePath = reader.IsDBNull(5) ? "" : reader.GetString(5)
                        });
                    }
                }
            }

            return Ok(result);
        }
    }

    public class ProductMenuDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Category { get; set; }
        public decimal Price { get; set; }
        public string ImagePath { get; set; }
    }
}