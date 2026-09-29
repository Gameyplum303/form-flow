using System.Net;
using System.Text.Json;
using FormFlow.Data.Models;

namespace FormFlow.Blazor.Services
{
    public class AuthService(HttpClient httpClient, AdminSession? session = null) : IAuthService
    {
        public const string PendingMessage = "Your account is waiting for an administrator's approval.";
        public const string VerifyEmailMessage = "Please verify your email address first. Open the link we emailed you.";
        public const string InvalidLinkMessage = "This link is invalid or has expired. Ask for a new one.";
        private const string Unreachable = "Could not reach the server. Please try again.";
        private const string TooMany = "Too many attempts. Wait a minute and try again.";

        public async Task<(LoginResponse? Login, string? Error)> LoginAsync(string username, string password)
        {
            try
            {
                var response = await httpClient.PostAsJsonAsync("api/auth/login", new LoginRequest { Username = username, Password = password });
                return response.StatusCode switch
                {
                    HttpStatusCode.OK => (await response.Content.ReadFromJsonAsync<LoginResponse>(), null),
                    HttpStatusCode.Unauthorized => (null, "Invalid username or password."),
                    HttpStatusCode.Forbidden => (null, await ReadReasonAsync(response) == SignInBlocks.EmailUnverified
                        ? VerifyEmailMessage
                        : PendingMessage),
                    HttpStatusCode.TooManyRequests => (null, "Too many sign-in attempts. Wait a minute and try again."),
                    _ => (null, $"Sign-in failed ({(int)response.StatusCode}). Please try again."),
                };
            }
            catch (HttpRequestException)
            {
                return (null, "Could not reach the server. Please try again.");
            }
        }

        public async Task<SignUpResult> SignUpAsync(SignUpRequest request)
        {
            try
            {
                var response = await httpClient.PostAsJsonAsync("api/auth/signup", request);
                switch (response.StatusCode)
                {
                    case HttpStatusCode.Created:
                        var created = await response.Content.ReadFromJsonAsync<SignUpResponse>();
                        return new SignUpResult(created?.Status ?? AccountStatuses.Pending, new Dictionary<string, string[]>(), null,
                            created?.EmailVerificationRequired ?? false);
                    case HttpStatusCode.BadRequest or HttpStatusCode.Conflict:
                        var errors = await ReadFieldErrorsAsync(response);
                        return errors.Count > 0
                            ? new SignUpResult(null, errors, null)
                            : SignUpResult.Failed("Check the form and try again.");
                    case HttpStatusCode.TooManyRequests:
                        return SignUpResult.Failed("Too many attempts. Wait a minute and try again.");
                    default:
                        return SignUpResult.Failed($"Sign-up failed ({(int)response.StatusCode}). Please try again.");
                }
            }
            catch (HttpRequestException)
            {
                return SignUpResult.Failed("Could not reach the server. Please try again.");
            }
        }

        public Task<string?> RequestPasswordResetAsync(string email) => SendEmailRequestAsync("api/auth/forgot-password", email);

        public Task<string?> ResendVerificationAsync(string email) => SendEmailRequestAsync("api/auth/resend-verification", email);

        private async Task<string?> SendEmailRequestAsync(string url, string email)
        {
            try
            {
                var response = await httpClient.PostAsJsonAsync(url, new EmailRequest { Email = email.Trim() });
                return response.StatusCode switch
                {
                    HttpStatusCode.Accepted => null,
                    HttpStatusCode.TooManyRequests => TooMany,
                    _ => $"Request failed ({(int)response.StatusCode}). Please try again.",
                };
            }
            catch (HttpRequestException)
            {
                return Unreachable;
            }
        }

        public async Task<(string? Status, string? Error)> VerifyEmailAsync(string token)
        {
            try
            {
                var response = await httpClient.PostAsJsonAsync("api/auth/verify-email", new VerifyEmailRequest { Token = token });
                return response.StatusCode switch
                {
                    HttpStatusCode.OK => ((await response.Content.ReadFromJsonAsync<VerifyEmailResponse>())?.Status ?? AccountStatuses.Active, null),
                    HttpStatusCode.BadRequest => (null, InvalidLinkMessage),
                    HttpStatusCode.TooManyRequests => (null, TooMany),
                    _ => (null, $"Verification failed ({(int)response.StatusCode}). Please try again."),
                };
            }
            catch (HttpRequestException)
            {
                return (null, Unreachable);
            }
        }

        public async Task<FormResult> ResetPasswordAsync(string token, string password)
        {
            try
            {
                var response = await httpClient.PostAsJsonAsync("api/auth/reset-password", new ResetPasswordRequest { Token = token, Password = password });
                switch (response.StatusCode)
                {
                    case HttpStatusCode.NoContent:
                        return FormResult.Success();
                    case HttpStatusCode.BadRequest:
                        var errors = await ReadFieldErrorsAsync(response);
                        return errors.Count > 0 ? FormResult.Invalid(errors) : FormResult.Failed(InvalidLinkMessage);
                    case HttpStatusCode.TooManyRequests:
                        return FormResult.Failed(TooMany);
                    default:
                        return FormResult.Failed($"The password could not be changed ({(int)response.StatusCode}). Please try again.");
                }
            }
            catch (HttpRequestException)
            {
                return FormResult.Failed(Unreachable);
            }
        }

        public async Task<FormResult> ChangePasswordAsync(string currentPassword, string newPassword)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, "api/auth/change-password")
                {
                    Content = JsonContent.Create(new ChangePasswordRequest { CurrentPassword = currentPassword, NewPassword = newPassword }),
                };
                if (session?.Token is { } token)
                {
                    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                }

                var response = await httpClient.SendAsync(request);
                switch (response.StatusCode)
                {
                    case HttpStatusCode.OK:
                        return FormResult.Success(await response.Content.ReadFromJsonAsync<LoginResponse>());
                    case HttpStatusCode.BadRequest:
                        var errors = await ReadFieldErrorsAsync(response);
                        return errors.Count > 0 ? FormResult.Invalid(errors) : FormResult.Failed("Check the form and try again.");
                    case HttpStatusCode.Unauthorized:
                        return FormResult.Failed("Your sign-in has ended. Sign in again to change your password.");
                    case HttpStatusCode.TooManyRequests:
                        return FormResult.Failed(TooMany);
                    default:
                        return FormResult.Failed($"The password could not be changed ({(int)response.StatusCode}). Please try again.");
                }
            }
            catch (HttpRequestException)
            {
                return FormResult.Failed(Unreachable);
            }
        }

        /// <summary>The "reason" a sign-in was refused (see <see cref="SignInBlocks"/>), if the problem has one.</summary>
        private static async Task<string?> ReadReasonAsync(HttpResponseMessage response)
        {
            try
            {
                using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                return doc.RootElement.ValueKind == JsonValueKind.Object
                    && doc.RootElement.TryGetProperty("reason", out var reason) && reason.ValueKind == JsonValueKind.String
                    ? reason.GetString()
                    : null;
            }
            catch (JsonException)
            {
                return null;
            }
        }

        /// <summary>Reads the "errors" object of a validation problem response.</summary>
        private static async Task<Dictionary<string, string[]>> ReadFieldErrorsAsync(HttpResponseMessage response)
        {
            var errors = new Dictionary<string, string[]>();
            try
            {
                using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                if (doc.RootElement.ValueKind == JsonValueKind.Object
                    && doc.RootElement.TryGetProperty("errors", out var fields)
                    && fields.ValueKind == JsonValueKind.Object)
                {
                    foreach (var field in fields.EnumerateObject().Where(f => f.Value.ValueKind == JsonValueKind.Array))
                    {
                        errors[field.Name] = field.Value.EnumerateArray()
                            .Where(e => e.ValueKind == JsonValueKind.String)
                            .Select(e => e.GetString()!)
                            .ToArray();
                    }
                }
            }
            catch (JsonException)
            {
                // Not a validation problem; the caller shows a general message.
            }
            return errors;
        }
    }
}
