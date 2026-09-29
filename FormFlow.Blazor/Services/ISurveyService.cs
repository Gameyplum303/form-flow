using FormFlow.Data.Models;

namespace FormFlow.Blazor.Services
{
    /// <summary>
    /// Outcome of submitting answers to a survey. <see cref="CanRetry"/> is false when fixing answers won't
    /// help: the survey closed, is gone, or this browser already answered it.
    /// </summary>
    public record SubmitResult(bool Success, IReadOnlyDictionary<string, string[]> Errors, string? Message = null, bool CanRetry = true);

    /// <summary>A downloaded CSV file.</summary>
    public record CsvExport(string FileName, byte[] Content);

    public interface ISurveyService
    {
        Task<List<SurveyDefinition>> GetSurveysAsync();
        Task<SurveyDefinition?> GetSurveyAsync(Guid id);

        /// <summary>Opens a survey from the code in its share link, or null when it isn't available.</summary>
        Task<SurveyDefinition?> GetSurveyByShareCodeAsync(string code);

        /// <summary>The surveys the signed-in account manages: every survey for an administrator, their own for a professor.</summary>
        Task<List<SurveyDefinition>> GetManagedSurveysAsync();

        /// <summary>The survey's questions, in survey order.</summary>
        Task<List<QuestionDefinition>> GetSurveyQuestionsAsync(Guid id);

        Task<(bool Success, string? Error)> CreateSurveyAsync(NewSurvey survey);
        Task<(bool Success, string? Error)> UpdateSurveyAsync(Guid id, NewSurvey survey);
        Task<(bool Success, string? Error)> DeleteSurveyAsync(Guid id);

        /// <summary>Publishes or unpublishes a survey, lists it or not, and sets when it closes.</summary>
        Task<(SurveyDefinition? Survey, string? Error)> UpdateSharingAsync(Guid id, SurveySharing sharing);

        /// <summary>Sends answers. The respondent id lets the server refuse a second answer from the same browser.</summary>
        Task<SubmitResult> SubmitResponseAsync(Guid surveyId, Dictionary<string, List<string>> answers, string? respondentId = null);

        /// <summary>Whether the browser with this respondent id already answered the survey.</summary>
        Task<bool> HasAnsweredAsync(Guid surveyId, string respondentId);

        /// <summary>Summaries of the survey's responses, narrowed or split by the query; null if refused or invalid.</summary>
        Task<SurveyResults?> GetResultsAsync(Guid surveyId, ResultsQuery? query = null);

        /// <summary>Downloads the responses (only those matching the query's filters and dates) as CSV, or null if refused.</summary>
        Task<CsvExport?> ExportResponsesAsync(Guid surveyId, ResultsQuery? query = null);
    }
}
