using NeoCoffeeServer.Models;

namespace NeoCoffeeServer.Data
{
    public static class DbSeeder
    {
        public static void Seed(AppDbContext db)
        {
            if (db.Users.Any())
                return;

            db.Users.AddRange(
                new User
                {
                    Login = "terminal1",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("1234"),
                    DisplayName = "Терминал #1",
                    Role = "OrderTerminal",
                    IsActive = true
                },
                new User
                {
                    Login = "barista1",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("1234"),
                    DisplayName = "Бариста #1",
                    Role = "Barista",
                    IsActive = true
                },
                new User
                {
                    Login = "tv1",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("1234"),
                    DisplayName = "Табло #1",
                    Role = "ReadyBoard",
                    IsActive = true
                },
                new User
                {
                    Login = "pickup1",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("1234"),
                    DisplayName = "Выдача #1",
                    Role = "Pickup",
                    IsActive = true
                },
                new User
                {
                    Login = "admin",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"),
                    DisplayName = "Администратор",
                    Role = "Admin",
                    IsActive = true
                }
            );

            db.SaveChanges();
        }
    }
}
