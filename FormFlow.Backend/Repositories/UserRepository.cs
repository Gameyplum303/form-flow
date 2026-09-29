using FormFlow.Backend.Auth;
using FormFlow.Data.Models;
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

        public AdminUser? FindById(Guid id) => _users.FindById(id);

        public AdminUser? FindByEmail(string email)
        {
            var normalized = Normalize(email);
            return normalized.Length == 0 ? null : _users.FindOne(u => u.Email != null && u.Email.ToLowerInvariant() == normalized);
        }

        public List<AdminUser> FindPending() =>
            _users.Find(u => u.Status == AccountStatuses.Pending).OrderBy(u => u.CreatedAt).ToList();

        public bool Update(AdminUser user) => _users.Update(user);

        public bool Delete(Guid id) => _users.Delete(id);

        public int Count() => _users.Count();

        private static string Normalize(string username) => username.Trim().ToLowerInvariant();
    }
}
