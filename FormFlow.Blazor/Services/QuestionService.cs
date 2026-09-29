using System.Net;
using FormFlow.Data.Models;

namespace FormFlow.Blazor.Services
{
    public class QuestionService(HttpClient httpClient, ILogger<QuestionService> logger, AdminSession? session = null) : IQuestionService
    {
        /// <summary>The API client, carrying the signed-in admin's token when there is one.</summary>
        private HttpClient Client
        {
            get
            {
                session?.Authorize(httpClient);
                return httpClient;
            }
        }

        public async Task<List<QuestionDefinition>?> GetAllQuestionsAsync()
        {
            try
            {
                var response = await Client.GetAsync("api/questions");
                if (!response.IsSuccessStatusCode)
                {
                    logger.LogWarning("Loading the questions failed with {Status}: {Body}",
                        (int)response.StatusCode, await response.Content.ReadAsStringAsync());
                    return null;
                }

                return await response.Content.ReadFromJsonAsync<List<QuestionDefinition>>() ?? [];
            }
            catch (HttpRequestException ex)
            {
                logger.LogWarning(ex, "Could not reach the API to load the questions.");
                return null;
            }
        }

        public Task<QuestionDefinition?> GetQuestionAsync(Guid id) =>
            ApiErrors.TryAsync(async () =>
            {
                var response = await Client.GetAsync($"api/questions/{id}");
                return response.StatusCode == HttpStatusCode.OK
                    ? await response.Content.ReadFromJsonAsync<QuestionDefinition>()
                    : null;
            }, null);

        public Task<(bool Success, string? Error)> CreateQuestionAsync(NewQuestion newQuestion) =>
            ApiErrors.SendAsync(() => Client.PostAsJsonAsync("api/questions", newQuestion));

        public Task<(bool Success, string? Error)> UpdateQuestionAsync(Guid id, NewQuestion question) =>
            ApiErrors.SendAsync(() => Client.PutAsJsonAsync($"api/questions/{id}", question));

        public Task<(bool Success, string? Error)> DeleteQuestionAsync(Guid id) =>
            ApiErrors.SendAsync(() => Client.DeleteAsync($"api/questions/{id}"));
    }
}
