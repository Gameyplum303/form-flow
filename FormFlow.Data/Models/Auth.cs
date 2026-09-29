namespace FormFlow.Data.Models
{
    /// <summary>Body of POST /api/auth/login.</summary>
    public class LoginRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    /// <summary>A signed-in account's bearer token.</summary>
    public class LoginResponse
    {
        public string Token { get; set; } = string.Empty;

        /// <summary>The account's id, which surveys and questions record as their owner.</summary>
        public Guid UserId { get; set; }
        public string Username { get; set; } = string.Empty;

        /// <summary>One of <see cref="Roles"/>.</summary>
        public string Role { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
    }
}
