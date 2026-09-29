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

    /// <summary>
    /// The survey API. Nothing here throws when the server can't be reached: reads return null and
    /// changes return an error message, so pages can show a friendly message instead.
    /// </summary>
    public interface ISurveyService
    {
        /// <summary>The published, listed surveys, or null when they could not be loaded.</summary>
        Task<List<SurveyDefinition>?> GetSurveysAsync();

        /// <summary>The survey, or null when it doesn't exist, isn't available to this account, or the server can't be reached.</summary>
        Task<SurveyDefinition?> GetSurveyAsync(Guid id);

        /// <summary>Opens a survey from the code in its share link, or null when it isn't available.</summary>
        Task<SurveyDefinition?> GetSurveyByShareCodeAsync(string code);

        /// <summary>
        /// The surveys the signed-in account manages: every survey for an administrator, their own for a professor.
        /// Null when they could not be loaded.
        /// </summary>
        Task<List<SurveyDefinition>?> GetManagedSurveysAsync();

        /// <summary>The survey's questions, in survey order, or null when they could not be loaded.</summary>
        Task<List<QuestionDefinition>?> GetSurveyQuestionsAsync(Guid id);

        Task<(bool Success, string? Error)> CreateSurveyAsync(NewSurvey survey);
        Task<(bool Success, string? Error)> UpdateSurveyAsync(Guid id, NewSurvey survey);
        Task<(bool Success, string? Error)> DeleteSurveyAsync(Guid id);

        /// <summary>Copies a survey into a new draft the signed-in account owns: the copy, or why it failed.</summary>
        Task<(SurveyDefinition? Survey, string? Error)> DuplicateSurveyAsync(Guid id);

        /// <summary>The ready-made surveys builders can start from, or null when they could not be loaded.</summary>
        Task<List<SurveyTemplate>?> GetTemplatesAsync();

        /// <summary>Starts a new draft from a template: the new survey, or why it failed.</summary>
        Task<(SurveyDefinition? Survey, string? Error)> UseTemplateAsync(Guid templateId);

        /// <summary>Publishes or unpublishes a survey, lists it or not, and sets when it closes.</summary>
        Task<(SurveyDefinition? Survey, string? Error)> UpdateSharingAsync(Guid id, SurveySharing sharing);

        /// <summary>Sends answers. The respondent id lets the server refuse a second answer from the same browser.</summary>
        Task<SubmitResult> SubmitResponseAsync(Guid surveyId, Dictionary<string, List<string>> answers, string? respondentId = null);

        /// <summary>Whether the browser with this respondent id already answered the survey (false when unknown).</summary>
        Task<bool> HasAnsweredAsync(Guid surveyId, string respondentId);

        /// <summary>Summaries of the survey's responses, narrowed or split by the query; null if refused or invalid.</summary>
        Task<SurveyResults?> GetResultsAsync(Guid surveyId, ResultsQuery? query = null);

        /// <summary>Downloads the responses (only those matching the query's filters and dates) as CSV, or null if refused.</summary>
        Task<CsvExport?> ExportResponsesAsync(Guid surveyId, ResultsQuery? query = null);
    }
}
