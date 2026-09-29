using System.Net;
using FormFlow.Data.Models;

namespace FormFlow.Blazor.Services
{
    public class AuthService(HttpClient httpClient) : IAuthService
    {
        public async Task<(LoginResponse? Login, string? Error)> LoginAsync(string username, string password)
        {
            try
            {
                var response = await httpClient.PostAsJsonAsync("api/auth/login", new LoginRequest { Username = username, Password = password });
                return response.StatusCode switch
                {
                    HttpStatusCode.OK => (await response.Content.ReadFromJsonAsync<LoginResponse>(), null),
                    HttpStatusCode.Unauthorized => (null, "Invalid username or password."),
                    HttpStatusCode.TooManyRequests => (null, "Too many sign-in attempts. Wait a minute and try again."),
                    _ => (null, $"Sign-in failed ({(int)response.StatusCode}). Please try again."),
                };
            }
            catch (HttpRequestException)
            {
                return (null, "Could not reach the server. Please try again.");
            }
        }
    }
}
