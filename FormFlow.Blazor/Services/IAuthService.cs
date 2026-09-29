using FormFlow.Data.Models;

namespace FormFlow.Blazor.Services
{
    public interface IAuthService
    {
        /// <summary>Returns the new sign-in, or an error message to show.</summary>
        Task<(LoginResponse? Login, string? Error)> LoginAsync(string username, string password);
    }
}
