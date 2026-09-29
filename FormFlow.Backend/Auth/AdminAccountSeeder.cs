using FormFlow.Backend.Repositories;
using Microsoft.AspNetCore.Identity;

namespace FormFlow.Backend.Auth
{
    /// <summary>
    /// Creates the accounts listed under AdminAccounts (each with a Username and Password) that don't
    /// exist yet. Existing accounts are left alone, so changing a password in configuration later has
    /// no effect. Passwords are only ever stored hashed.
    /// </summary>
    public class AdminAccountSeeder(IUserRepository users, IPasswordHasher<AdminUser> hasher, IConfiguration config,
        ILogger<AdminAccountSeeder> logger)
    {
        public const string Section = "AdminAccounts";

        public void Seed()
        {
            foreach (var account in config.GetSection(Section).GetChildren())
            {
                var username = account["Username"]?.Trim();
                var password = account["Password"];
                if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
                {
                    logger.LogWarning("Skipping {Section}:{Index}, which needs both a Username and a Password.", Section, account.Key);
                    continue;
                }

                if (users.FindByUsername(username) is not null)
                {
                    continue;
                }

                var user = new AdminUser { Id = Guid.NewGuid(), Username = username, CreatedAt = DateTime.UtcNow };
                user.PasswordHash = hasher.HashPassword(user, password);
                users.Insert(user);
                logger.LogInformation("Created admin account '{Username}'.", user.Username);
            }

            if (users.Count() == 0)
            {
                logger.LogWarning("No admin account exists. Add one under {Section} to sign in.", Section);
            }
        }
    }
}
