using LiteDB;

namespace FormFlow.Backend.Auth
{
    /// <summary>An account that can sign in to the admin pages. Passwords are stored as ASP.NET Core Identity hashes.</summary>
    public class AdminUser
    {
        [BsonId]
        public Guid Id { get; set; }
        /// <summary>The name as it was created, shown after sign-in.</summary>
        public string Username { get; set; } = string.Empty;

        /// <summary>Trimmed, lower-case <see cref="Username"/>, so sign-in ignores case.</summary>
        public string NormalizedUsername { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;

        /// <summary>
        /// <see cref="JwtSettings.AdminRole"/> or <see cref="JwtSettings.ViewerRole"/>. Accounts stored before
        /// roles existed were all admins, so that is the default when the field is missing.
        /// </summary>
        public string Role { get; set; } = JwtSettings.AdminRole;
        public DateTime CreatedAt { get; set; }
    }
}
