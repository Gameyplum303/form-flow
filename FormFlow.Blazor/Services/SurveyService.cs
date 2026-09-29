using System.Net;
using System.Text.Json;
using FormFlow.Data.Models;

namespace FormFlow.Blazor.Services
{
    public class SurveyService(HttpClient httpClient) : ISurveyService
    {
        public async Task<List<SurveyDefinition>> GetSurveysAsync() =>
            await httpClient.GetFromJsonAsync<List<SurveyDefinition>>("api/surveys") ?? [];

        public async Task<SurveyDefinition?> GetSurveyAsync(Guid id)
        {
            var response = await httpClient.GetAsync($"api/surveys/{id}");
            return response.StatusCode == HttpStatusCode.OK
                ? await response.Content.ReadFromJsonAsync<SurveyDefinition>()
                : null;
        }

        public async Task<List<QuestionDefinition>> GetSurveyQuestionsAsync(Guid id)
        {
            var response = await httpClient.GetAsync($"api/surveys/{id}/questions");
            return response.StatusCode == HttpStatusCode.OK
                ? await response.Content.ReadFromJsonAsync<List<QuestionDefinition>>() ?? []
                : [];
        }

        public async Task<(bool Success, string? Error)> CreateSurveyAsync(NewSurvey survey)
        {
            var response = await httpClient.PostAsJsonAsync("api/surveys", survey);
            return response.IsSuccessStatusCode ? (true, null) : (false, await ReadErrorAsync(response));
        }

        public async Task<(bool Success, string? Error)> UpdateSurveyAsync(Guid id, NewSurvey survey)
        {
            var response = await httpClient.PutAsJsonAsync($"api/surveys/{id}", survey);
            return response.IsSuccessStatusCode ? (true, null) : (false, await ReadErrorAsync(response));
        }

        public async Task<(bool Success, string? Error)> DeleteSurveyAsync(Guid id)
        {
            var response = await httpClient.DeleteAsync($"api/surveys/{id}");
            return response.IsSuccessStatusCode ? (true, null) : (false, await ReadErrorAsync(response));
        }

        public async Task<SubmitResult> SubmitResponseAsync(Guid surveyId, Dictionary<string, List<string>> answers)
        {
            try
            {
                var response = await httpClient.PostAsJsonAsync($"api/surveys/{surveyId}/responses", new { answers });
                if (response.IsSuccessStatusCode)
                {
                    return new SubmitResult(true, new Dictionary<string, string[]>());
                }

                if (response.StatusCode == HttpStatusCode.BadRequest)
                {
                    var problem = await response.Content.ReadFromJsonAsync<ValidationProblem>();
                    if (problem?.Errors is { Count: > 0 })
                    {
                        return new SubmitResult(false, problem.Errors, "Please fix the highlighted answers.");
                    }
                }

                return new SubmitResult(false, new Dictionary<string, string[]>(), await ReadErrorAsync(response));
            }
            catch (HttpRequestException)
            {
                return new SubmitResult(false, new Dictionary<string, string[]>(), "Could not reach the server. Please try again.");
            }
        }

        public async Task<SurveyResults?> GetResultsAsync(Guid surveyId)
        {
            var response = await httpClient.GetAsync($"api/surveys/{surveyId}/results");
            return response.StatusCode == HttpStatusCode.OK
                ? await response.Content.ReadFromJsonAsync<SurveyResults>()
                : null;
        }

        public string ExportUrl(Guid surveyId) =>
            new Uri(httpClient.BaseAddress!, $"api/surveys/{surveyId}/responses/export").ToString();

        private static async Task<string> ReadErrorAsync(HttpResponseMessage response)
        {
            var body = await response.Content.ReadAsStringAsync();
            try
            {
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                    doc.RootElement.TryGetProperty("error", out var error) &&
                    error.ValueKind == JsonValueKind.String)
                {
                    return error.GetString()!;
                }
            }
            catch (JsonException)
            {
                // Not JSON; fall through to the raw body.
            }
            return string.IsNullOrWhiteSpace(body) ? $"Request failed ({(int)response.StatusCode})" : body;
        }

        private sealed class ValidationProblem
        {
            public Dictionary<string, string[]>? Errors { get; set; }
        }
    }
}
