using FormFlow.Data.Models;

namespace FormFlow.Blazor.Services
{
    /// <summary>Lets administrators review professor/scientist sign-ups.</summary>
    public interface IAccountService
    {
        Task<List<PendingAccount>> GetPendingAsync();

        /// <summary>Returns null on success, or a message to show.</summary>
        Task<string?> ApproveAsync(Guid id);

        /// <summary>Removes the sign-up. Returns null on success, or a message to show.</summary>
        Task<string?> DeclineAsync(Guid id);
    }
}
