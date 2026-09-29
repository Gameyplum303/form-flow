using System.Security.Cryptography;
using System.Text;
using FormFlow.Data.Models;
using Microsoft.IdentityModel.Tokens;

namespace FormFlow.Backend.Auth
{
    /// <summary>Settings for the tokens the API issues, read from the "Jwt" configuration section.</summary>
    public class JwtSettings
    {
        /// <summary>Administrators only, for managing accounts.</summary>
        public const string AdminPolicy = "Admin";

        /// <summary>
        /// Building questions and surveys: administrators and professors. Endpoints then check that a
        /// professor owns what they change or read (see <see cref="CurrentUser.CanManage"/>).
        /// </summary>
        public const string BuilderPolicy = "Builder";

        /// <summary>Any signed-in account.</summary>
        public const string SignedInPolicy = "SignedIn";

        public string Issuer { get; init; } = "FormFlow";
        public string Audience { get; init; } = "FormFlow";
        public TimeSpan Lifetime { get; init; } = TimeSpan.FromHours(8);
        public required SymmetricSecurityKey SigningKey { get; init; }

        /// <summary>
        /// Uses Jwt:Key when it is at least 32 bytes. Otherwise a random key is generated, which
        /// is secure but signs everyone out whenever the API restarts.
        /// </summary>
        public static JwtSettings FromConfiguration(IConfiguration config, ILogger logger)
        {
            var section = config.GetSection("Jwt");
            var configuredKey = section["Key"];
            byte[] keyBytes;
            if (!string.IsNullOrEmpty(configuredKey) && Encoding.UTF8.GetByteCount(configuredKey) >= 32)
            {
                keyBytes = Encoding.UTF8.GetBytes(configuredKey);
            }
            else
            {
                logger.LogWarning("Jwt:Key is not set or shorter than 32 bytes, so a random signing key is used. Sign-ins end when the API restarts.");
                keyBytes = RandomNumberGenerator.GetBytes(64);
            }

            return new JwtSettings
            {
                Issuer = section["Issuer"] ?? "FormFlow",
                Audience = section["Audience"] ?? "FormFlow",
                Lifetime = TimeSpan.FromMinutes(section.GetValue("LifetimeMinutes", 480)),
                SigningKey = new SymmetricSecurityKey(keyBytes),
            };
        }

        public TokenValidationParameters ValidationParameters() => new()
        {
            ValidIssuer = Issuer,
            ValidAudience = Audience,
            IssuerSigningKey = SigningKey,
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
        };
    }
}
