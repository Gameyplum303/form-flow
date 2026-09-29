using System.Net;
using FormFlow.Data.Models;

namespace FormFlow.Blazor.Services
{
    public class AccountService(HttpClient httpClient, AdminSession session) : IAccountService
    {
        private HttpClient Client
        {
            get
            {
                session.Authorize(httpClient);
                return httpClient;
            }
        }

        public async Task<List<PendingAccount>> GetPendingAsync() =>
            await Client.GetFromJsonAsync<List<PendingAccount>>("api/accounts/pending") ?? [];

        public async Task<List<SentEmail>?> GetOutboxAsync()
        {
            var response = await Client.GetAsync("api/accounts/outbox");
            return response.StatusCode == HttpStatusCode.OK
                ? await response.Content.ReadFromJsonAsync<List<SentEmail>>() ?? []
                : null;
        }

        public Task<string?> ApproveAsync(Guid id) => PostAsync($"api/accounts/{id}/approve");

        public Task<string?> DeclineAsync(Guid id) => PostAsync($"api/accounts/{id}/decline");

        private async Task<string?> PostAsync(string url)
        {
            var response = await Client.PostAsync(url, null);
            return response.StatusCode switch
            {
                HttpStatusCode.NoContent => null,
                HttpStatusCode.NotFound => "That sign-up is no longer waiting. Another administrator may have handled it.",
                HttpStatusCode.Forbidden => "Only administrators can review sign-ups.",
                _ => $"Request failed ({(int)response.StatusCode}). Please try again.",
            };
        }
    }
}
