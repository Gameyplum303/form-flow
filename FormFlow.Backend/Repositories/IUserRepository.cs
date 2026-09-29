using FormFlow.Backend.Auth;

namespace FormFlow.Backend.Repositories
{
    public interface IUserRepository
    {
        AdminUser Insert(AdminUser user);
        /// <summary>Finds a user by name, ignoring case.</summary>
        AdminUser? FindByUsername(string username);
        AdminUser? FindById(Guid id);

        /// <summary>Finds the account with this email address, ignoring case.</summary>
        AdminUser? FindByEmail(string email);

        /// <summary>Accounts waiting for approval, oldest first.</summary>
        List<AdminUser> FindPending();
        bool Update(AdminUser user);
        bool Delete(Guid id);
        int Count();
    }
}
