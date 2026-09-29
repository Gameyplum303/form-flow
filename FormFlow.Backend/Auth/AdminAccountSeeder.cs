using FormFlow.Backend.Repositories;
using FormFlow.Data.Models;
using Microsoft.AspNetCore.Identity;

namespace FormFlow.Backend.Auth
{
    /// <summary>
    /// Creates the accounts listed under Accounts (each with a Username, Password and Role) that don't
    /// exist yet, and keeps each listed account's role in step with configuration. Passwords of existing
    /// accounts are never changed. Passwords are only ever stored hashed. An account without a Role is a
    /// student, the role with the least access.
    /// </summary>
    public class AdminAccountSeeder(IUserRepository users, IPasswordHasher<AdminUser> hasher, IConfiguration config,
        ILogger<AdminAccountSeeder> logger)
    {
        public const string Section = "Accounts";

        public void Seed()
        {
            foreach (var account in config.GetSection(Section).GetChildren())
            {
                var username = account["Username"]?.Trim();
                var password = account["Password"];
                var role = account["Role"]?.Trim().ToLowerInvariant() ?? Roles.Student;
                if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
                {
                    logger.LogWarning("Skipping {Section}:{Index}, which needs both a Username and a Password.", Section, account.Key);
                    continue;
                }
                if (!Roles.IsKnown(role))
                {
                    logger.LogWarning("Skipping account '{Username}': Role must be one of {Roles}.",
                        username, string.Join(", ", Roles.All));
                    continue;
                }

                var existing = users.FindByUsername(username);
                if (existing is not null)
                {
                    if (existing.Role != role)
                    {
                        existing.Role = role;
                        users.Update(existing);
                        logger.LogInformation("Account '{Username}' is now a {Role}.", existing.Username, role);
                    }
                    continue;
                }

                var user = new AdminUser { Id = Guid.NewGuid(), Username = username, Role = role, CreatedAt = DateTime.UtcNow };
                user.PasswordHash = hasher.HashPassword(user, password);
                users.Insert(user);
                logger.LogInformation("Created {Role} account '{Username}'.", role, user.Username);
            }

            if (users.Count() == 0)
            {
                logger.LogWarning("No accounts exist. Add one under {Section} to sign in.", Section);
            }
        }
    }
}
