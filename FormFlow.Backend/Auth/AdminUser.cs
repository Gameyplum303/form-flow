using LiteDB;

namespace FormFlow.Backend.Auth
{
    /// <summary>An account that can use the admin endpoints. Passwords are stored as ASP.NET Core Identity hashes.</summary>
    public class AdminUser
    {
        [BsonId]
        public Guid Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
}
