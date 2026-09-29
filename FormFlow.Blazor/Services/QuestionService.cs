
using System.Net;
using System.Text.Json;
using FormFlow.Data.Models;

namespace FormFlow.Blazor.Services
{
    public class QuestionService(HttpClient httpClient, AdminSession? session = null) : IQuestionService
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
            var response = await Client.GetAsync("/api/questions");

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"API ERROR: {(int)response.StatusCode} {response.ReasonPhrase}");
                Console.WriteLine($"Body: {errorContent}");

                return new List<QuestionDefinition>();
            }

            return await response.Content.ReadFromJsonAsync<List<QuestionDefinition>>()
                ?? new List<QuestionDefinition>();
        }

        public async Task<QuestionDefinition?> GetQuestionAsync(Guid id)
        {
            var response = await Client.GetAsync($"/api/questions/{id}");
            return response.StatusCode == HttpStatusCode.OK
                ? await response.Content.ReadFromJsonAsync<QuestionDefinition>()
                : null;
        }

        public Task<(bool Success, string? Error)> CreateQuestionAsync(NewQuestion newQuestion) =>
            SendAsync(() => Client.PostAsJsonAsync("/api/questions", newQuestion));

        public Task<(bool Success, string? Error)> UpdateQuestionAsync(Guid id, NewQuestion question) =>
            SendAsync(() => Client.PutAsJsonAsync($"/api/questions/{id}", question));

        public Task<(bool Success, string? Error)> DeleteQuestionAsync(Guid id) =>
            SendAsync(() => Client.DeleteAsync($"/api/questions/{id}"));

        private static async Task<(bool Success, string? Error)> SendAsync(Func<Task<HttpResponseMessage>> send)
        {
            try
            {
                var response = await send();
                if (response.IsSuccessStatusCode)
                {
                    return (true, null);
                }
                var body = await response.Content.ReadAsStringAsync();
                return (false, $"{(int)response.StatusCode}: {DescribeError(body)}");
            }
            catch (HttpRequestException ex)
            {
                return (false, $"Could not reach the server: {ex.Message}");
            }
        }

        /// <summary>
        /// Pulls the human-readable message out of the API's error shapes:
        /// { "error": "..." }, { "errors": ["..."] }, or a bare JSON string.
        /// </summary>
        internal static string DescribeError(string body)
        {
            try
            {
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;
                if (root.ValueKind == JsonValueKind.String)
                {
                    return root.GetString()!;
                }
                if (root.ValueKind == JsonValueKind.Object)
                {
                    if (root.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.String)
                    {
                        return error.GetString()!;
                    }
                    if (root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Array)
                    {
                        return string.Join(" ", errors.EnumerateArray().Select(e => e.ToString()));
                    }
                }
            }
            catch (JsonException)
            {
                // Not JSON; show the body as is.
            }
            return body;
        }
    }
}
