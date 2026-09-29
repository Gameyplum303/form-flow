using System.Net.Http.Headers;
using System.Net.Http.Json;
using FormFlow.Data.Models;

namespace FormFlow.Backend.Tests.Endpoints
{
    /// <summary>
    /// Signs a test client in with the accounts appsettings.Development.json creates: Rogers is an
    /// admin, and student is a view-only account.
    /// </summary>
    public static class AdminClient
    {
        public const string Username = "Rogers";
        public const string Password = "password";
        public const string ViewerUsername = "student";
        public const string ViewerPassword = "password";

        public static async Task<LoginResponse> LoginAsync(HttpClient client, string username = Username, string password = Password)
        {
            var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Username = username, Password = password });
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<LoginResponse>())!;
        }

        public static HttpClient AsAdmin(this HttpClient client) => client.SignedInAs(Username, Password);

        public static HttpClient AsViewer(this HttpClient client) => client.SignedInAs(ViewerUsername, ViewerPassword);

        private static HttpClient SignedInAs(this HttpClient client, string username, string password)
        {
            var login = LoginAsync(client, username, password).GetAwaiter().GetResult();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.Token);
            return client;
        }
    }
}
