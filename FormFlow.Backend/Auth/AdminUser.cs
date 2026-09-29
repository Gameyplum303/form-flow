using FormFlow.Data.Models;
using LiteDB;

namespace FormFlow.Backend.Auth
{
    /// <summary>An account that can sign in. Passwords are stored as ASP.NET Core Identity hashes.</summary>
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
        /// One of <see cref="Roles"/>. Accounts stored before roles existed were all admins, so that is
        /// the default when the field is missing.
        /// </summary>
        public string Role { get; set; } = Roles.Admin;

        /// <summary>One of <see cref="AccountStatuses"/>. Accounts stored before sign-up existed are active.</summary>
        public string Status { get; set; } = AccountStatuses.Active;
        public DateTime CreatedAt { get; set; }

        // Filled in by professors and scientists when they sign up; empty for accounts from configuration.
        public string? Name { get; set; }
        public string? Email { get; set; }

        /// <summary>An ISO date (YYYY-MM-DD). Stored as text because LiteDB has no date-only type.</summary>
        public string? DateOfBirth { get; set; }
        public string? IntendedUse { get; set; }
        public string? Organization { get; set; }

        /// <summary>
        /// Whether the email address is confirmed. Sign-ups start unverified; accounts from configuration
        /// and accounts stored before verification existed count as verified.
        /// </summary>
        public bool EmailVerified { get; set; } = true;

        /// <summary>When the password last changed. Sign-in tokens issued before then stop working.</summary>
        public DateTime? PasswordChangedAt { get; set; }
    }
}
