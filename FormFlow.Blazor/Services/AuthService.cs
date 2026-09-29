using System.Net;
using System.Text.Json;
using FormFlow.Data.Models;

namespace FormFlow.Blazor.Services
{
    public class AuthService(HttpClient httpClient) : IAuthService
    {
        public const string PendingMessage = "Your account is waiting for an administrator's approval.";

        public async Task<(LoginResponse? Login, string? Error)> LoginAsync(string username, string password)
        {
            try
            {
                var response = await httpClient.PostAsJsonAsync("api/auth/login", new LoginRequest { Username = username, Password = password });
                return response.StatusCode switch
                {
                    HttpStatusCode.OK => (await response.Content.ReadFromJsonAsync<LoginResponse>(), null),
                    HttpStatusCode.Unauthorized => (null, "Invalid username or password."),
                    HttpStatusCode.Forbidden => (null, PendingMessage),
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
                        return new SignUpResult(created?.Status ?? AccountStatuses.Pending, new Dictionary<string, string[]>(), null);
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
