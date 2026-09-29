using FormFlow.Data.Models;

namespace FormFlow.Blazor.Services
{
    /// <summary>Outcome of submitting answers to a survey.</summary>
    public record SubmitResult(bool Success, IReadOnlyDictionary<string, string[]> Errors, string? Message = null);

    /// <summary>A downloaded CSV file.</summary>
    public record CsvExport(string FileName, byte[] Content);

    public interface ISurveyService
    {
        Task<List<SurveyDefinition>> GetSurveysAsync();
        Task<SurveyDefinition?> GetSurveyAsync(Guid id);

        /// <summary>The surveys the signed-in account manages: every survey for an administrator, their own for a professor.</summary>
        Task<List<SurveyDefinition>> GetManagedSurveysAsync();

        /// <summary>The survey's questions, in survey order.</summary>
        Task<List<QuestionDefinition>> GetSurveyQuestionsAsync(Guid id);

        Task<(bool Success, string? Error)> CreateSurveyAsync(NewSurvey survey);
        Task<(bool Success, string? Error)> UpdateSurveyAsync(Guid id, NewSurvey survey);
        Task<(bool Success, string? Error)> DeleteSurveyAsync(Guid id);

        Task<SubmitResult> SubmitResponseAsync(Guid surveyId, Dictionary<string, List<string>> answers);
        Task<SurveyResults?> GetResultsAsync(Guid surveyId);

        /// <summary>Downloads every response as CSV, or null if the survey is missing or the admin isn't signed in.</summary>
        Task<CsvExport?> ExportResponsesAsync(Guid surveyId);
    }
}
