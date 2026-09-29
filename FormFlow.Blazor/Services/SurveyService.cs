using System.Net;
using FormFlow.Data.Models;

namespace FormFlow.Blazor.Services
{
    public class SurveyService(HttpClient httpClient, AdminSession? session = null) : ISurveyService
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

        public Task<List<SurveyDefinition>?> GetSurveysAsync() => GetOrNullAsync<List<SurveyDefinition>>("api/surveys");

        public Task<List<SurveyDefinition>?> GetManagedSurveysAsync() => GetOrNullAsync<List<SurveyDefinition>>("api/surveys/managed");

        public Task<SurveyDefinition?> GetSurveyAsync(Guid id) => GetOrNullAsync<SurveyDefinition>($"api/surveys/{id}");

        public Task<SurveyDefinition?> GetSurveyByShareCodeAsync(string code) =>
            GetOrNullAsync<SurveyDefinition>($"api/share/{Uri.EscapeDataString(code)}");

        public Task<(SurveyDefinition? Survey, string? Error)> UpdateSharingAsync(Guid id, SurveySharing sharing) =>
            ApiErrors.TryAsync<(SurveyDefinition?, string?)>(async () =>
            {
                var response = await Client.PutAsJsonAsync($"api/surveys/{id}/sharing", sharing);
                return response.IsSuccessStatusCode
                    ? (await response.Content.ReadFromJsonAsync<SurveyDefinition>(), null)
                    : (null, await ApiErrors.ReadMessageAsync(response));
            }, (null, ApiErrors.Unreachable));

        public async Task<bool> HasAnsweredAsync(Guid surveyId, string respondentId) =>
            (await GetOrNullAsync<AnsweredResult>(
                $"api/surveys/{surveyId}/answered?respondentId={Uri.EscapeDataString(respondentId)}"))?.Answered == true;

        private sealed record AnsweredResult(bool Answered);

        public Task<List<QuestionDefinition>?> GetSurveyQuestionsAsync(Guid id) =>
            GetOrNullAsync<List<QuestionDefinition>>($"api/surveys/{id}/questions");

        public Task<(bool Success, string? Error)> CreateSurveyAsync(NewSurvey survey) =>
            ApiErrors.SendAsync(() => Client.PostAsJsonAsync("api/surveys", survey));

        public Task<(bool Success, string? Error)> UpdateSurveyAsync(Guid id, NewSurvey survey) =>
            ApiErrors.SendAsync(() => Client.PutAsJsonAsync($"api/surveys/{id}", survey));

        public Task<(bool Success, string? Error)> DeleteSurveyAsync(Guid id) =>
            ApiErrors.SendAsync(() => Client.DeleteAsync($"api/surveys/{id}"));

        public Task<SubmitResult> SubmitResponseAsync(Guid surveyId, Dictionary<string, List<string>> answers,
            string? respondentId = null) =>
            ApiErrors.TryAsync(async () =>
            {
                var response = await Client.PostAsJsonAsync($"api/surveys/{surveyId}/responses", new { answers, respondentId });
                if (response.IsSuccessStatusCode)
                {
                    return new SubmitResult(true, new Dictionary<string, string[]>());
                }

                if (response.StatusCode == HttpStatusCode.BadRequest)
                {
                    var errors = await ApiErrors.ReadFieldErrorsAsync(response);
                    if (errors.Count > 0)
                    {
                        return new SubmitResult(false, errors, "Please fix the highlighted answers.");
                    }
                }

                return response.StatusCode switch
                {
                    HttpStatusCode.NotFound => new SubmitResult(false, new Dictionary<string, string[]>(),
                        "This survey is no longer available.", CanRetry: false),
                    HttpStatusCode.Conflict => new SubmitResult(false, new Dictionary<string, string[]>(),
                        await ApiErrors.ReadMessageAsync(response), CanRetry: false),
                    _ => new SubmitResult(false, new Dictionary<string, string[]>(), await ApiErrors.ReadMessageAsync(response)),
                };
            }, new SubmitResult(false, new Dictionary<string, string[]>(), ApiErrors.Unreachable));

        public Task<SurveyResults?> GetResultsAsync(Guid surveyId, ResultsQuery? query = null) =>
            GetOrNullAsync<SurveyResults>(WithQuery($"api/surveys/{surveyId}/results", query));

        public Task<CsvExport?> ExportResponsesAsync(Guid surveyId, ResultsQuery? query = null) =>
            ApiErrors.TryAsync(async () =>
            {
                var response = await Client.GetAsync(WithQuery($"api/surveys/{surveyId}/responses/export", query));
                if (response.StatusCode != HttpStatusCode.OK)
                {
                    return null;
                }

                var disposition = response.Content.Headers.ContentDisposition;
                var fileName = (disposition?.FileNameStar ?? disposition?.FileName)?.Trim('"');
                return new CsvExport(string.IsNullOrWhiteSpace(fileName) ? "responses.csv" : fileName,
                    await response.Content.ReadAsByteArrayAsync());
            }, (CsvExport?)null);

        /// <summary>Reads the response to a GET, or null when the API refuses it, has nothing there, or can't be reached.</summary>
        private Task<T?> GetOrNullAsync<T>(string url) where T : class =>
            ApiErrors.TryAsync(async () =>
            {
                var response = await Client.GetAsync(url);
                return response.StatusCode == HttpStatusCode.OK
                    ? await response.Content.ReadFromJsonAsync<T>()
                    : null;
            }, (T?)null);

        private static string WithQuery(string path, ResultsQuery? query) =>
            query?.ToQueryString() is { Length: > 0 } queryString ? $"{path}?{queryString}" : path;
    }
}
