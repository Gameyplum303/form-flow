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
            else if (password != confirmation)
            {
                errors["confirmPassword"] = ["The passwords don't match."];
            }
            return errors;
        }
    }
}
