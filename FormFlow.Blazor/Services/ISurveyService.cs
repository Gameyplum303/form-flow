using FormFlow.Data.Models;

namespace FormFlow.Blazor.Services
{
    /// <summary>Outcome of submitting answers to a survey.</summary>
    public record SubmitResult(bool Success, IReadOnlyDictionary<string, string[]> Errors, string? Message = null);

    public interface ISurveyService
    {
        Task<List<SurveyDefinition>> GetSurveysAsync();
        Task<SurveyDefinition?> GetSurveyAsync(Guid id);

        /// <summary>The survey's questions, in survey order.</summary>
        Task<List<QuestionDefinition>> GetSurveyQuestionsAsync(Guid id);

        Task<(bool Success, string? Error)> CreateSurveyAsync(NewSurvey survey);
        Task<(bool Success, string? Error)> UpdateSurveyAsync(Guid id, NewSurvey survey);
        Task<(bool Success, string? Error)> DeleteSurveyAsync(Guid id);

        Task<SubmitResult> SubmitResponseAsync(Guid surveyId, Dictionary<string, List<string>> answers);
        Task<SurveyResults?> GetResultsAsync(Guid surveyId);

        /// <summary>Absolute URL of the CSV export, for a download link.</summary>
        string ExportUrl(Guid surveyId);
    }
}
