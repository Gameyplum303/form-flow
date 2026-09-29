using FormFlow.Data.Services;

namespace FormFlow.Blazor.Components.Shared
{
    /// <summary>The fields of a form that sets a new password, and the checks made before sending it.</summary>
    public sealed class NewPasswordForm
    {
        public string CurrentPassword { get; set; } = "";
        public string Password { get; set; } = "";
        public string ConfirmPassword { get; set; } = "";

        /// <summary>The server's password rules plus the confirmation, keyed by field name.</summary>
        public static Dictionary<string, string[]> Check(string password, string confirmation, string passwordField)
        {
            var errors = new Dictionary<string, string[]>();
            if (SignUpValidator.PasswordError(password) is { } error)
            {
                errors[passwordField] = [error];
            }
            else
            {
                CheckConfirmation(errors, password, confirmation);
            }
            return errors;
        }

        /// <summary>Adds a problem for the confirmation field when it doesn't repeat the password.</summary>
        public static void CheckConfirmation(IDictionary<string, string[]> errors, string password, string confirmation)
        {
            if (password != confirmation)
            {
                errors["confirmPassword"] = ["The passwords don't match."];
            }
        }
    }
}
