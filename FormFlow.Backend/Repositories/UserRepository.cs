using FormFlow.Backend.Auth;
using LiteDB;

namespace FormFlow.Backend.Repositories
{
    public class UserRepository : IUserRepository
    {
        public const string CollectionName = "users";

        private readonly ILiteCollection<AdminUser> _users;

        public UserRepository(ILiteDatabase db)
        {
            LiteDbMappings.EnsureBuilt();
            _users = db.GetCollection<AdminUser>(CollectionName);
            _users.EnsureIndex(u => u.NormalizedUsername, true);
        }

        public AdminUser Insert(AdminUser user)
        {
            user.Username = user.Username.Trim();
            user.NormalizedUsername = Normalize(user.Username);
            _users.Insert(user);
            return user;
        }

        public AdminUser? FindByUsername(string username) =>
            _users.FindOne(u => u.NormalizedUsername == Normalize(username));

        public int Count() => _users.Count();

        private static string Normalize(string username) => username.Trim().ToLowerInvariant();
    }
}
