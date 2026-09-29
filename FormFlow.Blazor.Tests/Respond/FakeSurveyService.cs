using FormFlow.Blazor.Services;
using FormFlow.Data.Models;

namespace FormFlow.Blazor.Tests.Respond;

/// <summary>In-memory ISurveyService for page tests.</summary>
public sealed class FakeSurveyService : ISurveyService
{
    public List<SurveyDefinition> Surveys { get; } = new();
    public Dictionary<Guid, List<QuestionDefinition>> Questions { get; } = new();
    public SubmitResult NextSubmitResult { get; set; } = new(true, new Dictionary<string, string[]>());
    public Dictionary<string, List<string>>? LastSubmitted { get; private set; }
    public SurveyResults? Results { get; set; }

    public Task<List<SurveyDefinition>> GetSurveysAsync() => Task.FromResult(Surveys.ToList());

    public Task<SurveyDefinition?> GetSurveyAsync(Guid id) =>
        Task.FromResult(Surveys.FirstOrDefault(s => s.Id == id));

    public Task<List<QuestionDefinition>> GetSurveyQuestionsAsync(Guid id) =>
        Task.FromResult(Questions.TryGetValue(id, out var q) ? q : new List<QuestionDefinition>());

    public Task<(bool Success, string? Error)> CreateSurveyAsync(NewSurvey survey) => Task.FromResult((true, (string?)null));

    public Task<(bool Success, string? Error)> UpdateSurveyAsync(Guid id, NewSurvey survey) => Task.FromResult((true, (string?)null));

    public Task<(bool Success, string? Error)> DeleteSurveyAsync(Guid id) => Task.FromResult((true, (string?)null));

    public Task<SubmitResult> SubmitResponseAsync(Guid surveyId, Dictionary<string, List<string>> answers)
    {
        LastSubmitted = answers.ToDictionary(a => a.Key, a => a.Value.ToList());
        return Task.FromResult(NextSubmitResult);
    }

    public Task<SurveyResults?> GetResultsAsync(Guid surveyId) => Task.FromResult(Results);

    public string ExportUrl(Guid surveyId) => $"http://localhost/api/surveys/{surveyId}/responses/export";
}
