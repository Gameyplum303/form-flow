using FormFlow.Data.Models;

namespace FormFlow.Blazor.Services
{
    public interface IAuthService
    {
        /// <summary>Returns the new sign-in, or an error message to show.</summary>
        Task<(LoginResponse? Login, string? Error)> LoginAsync(string username, string password);

        /// <summary>Asks for a professor/scientist account.</summary>
        Task<SignUpResult> SignUpAsync(SignUpRequest request);

        /// <summary>Emails a password reset link if an account uses the address. Returns null, or an error to show.</summary>
        Task<string?> RequestPasswordResetAsync(string email);

        /// <summary>Emails a new verification link if an unverified account uses the address. Returns null, or an error to show.</summary>
        Task<string?> ResendVerificationAsync(string email);

        /// <summary>Uses the token from a verification link. Returns the account's status, or an error to show.</summary>
        Task<(string? Status, string? Error)> VerifyEmailAsync(string token);

        /// <summary>Sets a new password with the token from a reset link.</summary>
        Task<FormResult> ResetPasswordAsync(string token, string password);

        /// <summary>Changes the signed-in account's password. On success, <see cref="FormResult.Login"/> is the new sign-in.</summary>
        Task<FormResult> ChangePasswordAsync(string currentPassword, string newPassword);
    }

    /// <summary>
    /// The outcome of a form sent to the API: success (with a new sign-in, for a password change), or
    /// problems keyed by field name, or a message for problems that belong to no field.
    /// </summary>
    public record FormResult(bool Succeeded, IReadOnlyDictionary<string, string[]> FieldErrors, string? Error, LoginResponse? Login = null)
    {
        public static FormResult Success(LoginResponse? login = null) => new(true, new Dictionary<string, string[]>(), null, login);

        public static FormResult Failed(string error) => new(false, new Dictionary<string, string[]>(), error);

        public static FormResult Invalid(IReadOnlyDictionary<string, string[]> fieldErrors) => new(false, fieldErrors, null);
    }

    /// <summary>
    /// The outcome of a sign-up: the new account's status (see <see cref="AccountStatuses"/>), or
    /// problems keyed by field name, or a message for problems that belong to no field.
    /// </summary>
    public record SignUpResult(string? Status, IReadOnlyDictionary<string, string[]> FieldErrors, string? Error,
        bool EmailVerificationRequired = false)
    {
        public bool Succeeded => Status is not null;

        public static SignUpResult Failed(string error) => new(null, new Dictionary<string, string[]>(), error);
    }
}
