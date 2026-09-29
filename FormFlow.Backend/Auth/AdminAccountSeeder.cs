using FormFlow.Backend.Repositories;
using Microsoft.AspNetCore.Identity;

namespace FormFlow.Backend.Auth
{
    /// <summary>
    /// Creates the first admin account from Admin:Username and Admin:Password when there are no
    /// accounts yet. The password is only ever stored hashed.
    /// </summary>
    public class AdminAccountSeeder(IUserRepository users, IPasswordHasher<AdminUser> hasher, IConfiguration config,
        ILogger<AdminAccountSeeder> logger)
    {
        public void Seed()
        {
            if (users.Count() > 0)
            {
                return;
            }

            var username = config["Admin:Username"];
            var password = config["Admin:Password"];
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                logger.LogWarning("No admin account exists. Set Admin:Username and Admin:Password to create one.");
                return;
            }

            var user = new AdminUser { Id = Guid.NewGuid(), Username = username, CreatedAt = DateTime.UtcNow };
            user.PasswordHash = hasher.HashPassword(user, password);
            users.Insert(user);
            logger.LogInformation("Created admin account '{Username}'.", user.Username);
        }
    }
}
