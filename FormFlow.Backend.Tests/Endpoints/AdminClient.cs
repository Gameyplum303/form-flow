using System.Net.Http.Headers;
using System.Net.Http.Json;
using FormFlow.Data.Models;

namespace FormFlow.Backend.Tests.Endpoints
{
    /// <summary>
    /// Signs a test client in as the test account that appsettings.Development.json creates,
    /// so tests can call the admin endpoints.
    /// </summary>
    public static class AdminClient
    {
        public const string Username = "student";
        public const string Password = "password";

        public static async Task<LoginResponse> LoginAsync(HttpClient client, string username = Username, string password = Password)
        {
            var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Username = username, Password = password });
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<LoginResponse>())!;
        }

        public static HttpClient AsAdmin(this HttpClient client)
        {
            var login = LoginAsync(client).GetAwaiter().GetResult();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.Token);
            return client;
        }
    }
}
