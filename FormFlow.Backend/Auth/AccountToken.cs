using LiteDB;

namespace FormFlow.Backend.Auth
{
    /// <summary>
    /// A single-use token from an emailed link (password reset or email verification). Only a SHA-256
    /// hash of the token is stored, so a copy of the database can't be used to take over accounts.
    /// </summary>
    public class AccountToken
    {
        [BsonId]
        public Guid Id { get; set; }
        public Guid UserId { get; set; }

        /// <summary>One of <see cref="AccountTokenPurposes"/>.</summary>
        public string Purpose { get; set; } = string.Empty;

        /// <summary>Lower-case hex SHA-256 of the token in the link.</summary>
        public string TokenHash { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
    }

    public static class AccountTokenPurposes
    {
        public const string ResetPassword = "reset_password";
        public const string VerifyEmail = "verify_email";
    }
}
