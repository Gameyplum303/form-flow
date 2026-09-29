using System.Globalization;
using FormFlow.Data.Models;

namespace FormFlow.Data.Services;

/// <summary>
/// Checks a professor/scientist sign-up. Errors are keyed by field name (as sent in JSON), so a form can
/// show each one under its field.
/// </summary>
public static class SignUpValidator
{
    public const int MinimumAge = 18;
    public const int MinimumPasswordLength = 8;
    public const int MaxNameLength = 100;
    public const int MaxOrganizationLength = 200;
    public const int MaxIntendedUseLength = 1000;

    public static Dictionary<string, string[]> Validate(SignUpRequest request, DateOnly today)
    {
        var errors = new Dictionary<string, string[]>();

        Required(errors, "name", request.Name, "Enter your name.", MaxNameLength);

        var email = request.Email?.Trim() ?? string.Empty;
        if (email.Length == 0)
        {
            errors["email"] = ["Enter your email address."];
        }
        else if (!ResponseValidator.IsEmail(email))
        {
            errors["email"] = ["Enter an email address, like name@example.com."];
        }

        var password = request.Password ?? string.Empty;
        if (password.Length < MinimumPasswordLength)
        {
            errors["password"] = [$"Use at least {MinimumPasswordLength} characters."];
        }
        else if (password.Length > 128)
        {
            errors["password"] = ["Use at most 128 characters."];
        }

        if (!DateOnly.TryParseExact(request.DateOfBirth?.Trim(), ResponseValidator.DateFormat, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var born))
        {
            errors["dateOfBirth"] = ["Enter your date of birth."];
        }
        else if (born > today || born.Year < 1900)
        {
            errors["dateOfBirth"] = ["Enter a real date of birth."];
        }
        else if (born.AddYears(MinimumAge) > today)
        {
            errors["dateOfBirth"] = [$"You must be at least {MinimumAge} to sign up."];
        }

        Required(errors, "intendedUse", request.IntendedUse, "Tell us how you'll use FormFlow.", MaxIntendedUseLength);
        Required(errors, "organization", request.Organization, "Enter your university, lab or company.", MaxOrganizationLength);

        return errors;
    }

    private static void Required(Dictionary<string, string[]> errors, string field, string? value, string missing, int maxLength)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            errors[field] = [missing];
        }
        else if (trimmed.Length > maxLength)
        {
            errors[field] = [$"Use at most {maxLength} characters."];
        }
    }
}
