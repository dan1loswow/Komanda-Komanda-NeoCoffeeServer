namespace NeoCoffeeServer.Models
{
    public class User
    {
        public int Id { get; set; }

        public string Login { get; set; } = string.Empty;

        public string PasswordHash { get; set; } = string.Empty;

        public string DisplayName { get; set; } = string.Empty;

        public string Role { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;
    }
}
