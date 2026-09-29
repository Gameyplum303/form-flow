using FormFlow.Backend.Auth;

namespace FormFlow.Backend.Repositories
{
    public interface IUserRepository
    {
        AdminUser Insert(AdminUser user);
        /// <summary>Finds a user by name, ignoring case.</summary>
        AdminUser? FindByUsername(string username);
        int Count();
    }
}
