using FormFlow.Backend.Auth;

namespace FormFlow.Backend.Repositories
{
    public interface IUserRepository
    {
        AdminUser Insert(AdminUser user);
        /// <summary>Finds a user by name, ignoring case.</summary>
        AdminUser? FindByUsername(string username);
        AdminUser? FindById(Guid id);

        /// <summary>Accounts waiting for approval, oldest first.</summary>
        List<AdminUser> FindPending();
        bool Update(AdminUser user);
        bool Delete(Guid id);
        int Count();
    }
}
