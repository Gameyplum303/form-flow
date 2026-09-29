using FormFlow.Data.Models;

namespace FormFlow.Blazor.Services
{
    public interface IAuthService
    {
        /// <summary>Returns the new sign-in, or an error message to show.</summary>
        Task<(LoginResponse? Login, string? Error)> LoginAsync(string username, string password);

        /// <summary>Asks for a professor/scientist account.</summary>
        Task<SignUpResult> SignUpAsync(SignUpRequest request);
    }

    /// <summary>
    /// The outcome of a sign-up: the new account's status (see <see cref="AccountStatuses"/>), or
    /// problems keyed by field name, or a message for problems that belong to no field.
    /// </summary>
    public record SignUpResult(string? Status, IReadOnlyDictionary<string, string[]> FieldErrors, string? Error)
    {
        public bool Succeeded => Status is not null;

        public static SignUpResult Failed(string error) => new(null, new Dictionary<string, string[]>(), error);
    }
}
