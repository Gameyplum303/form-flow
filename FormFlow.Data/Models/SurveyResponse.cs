using LiteDB;

namespace FormFlow.Data.Models
{
    /// <summary>
    /// One person's submitted answers to a survey.
    /// </summary>
    public class SurveyResponse
    {
        [BsonId]
        public Guid Id { get; set; }

        public Guid SurveyId { get; set; }

        public DateTime SubmittedAt { get; set; }

        /// <summary>The signed-in account that submitted it, or null for an anonymous response.</summary>
        public string? SubmittedBy { get; set; }

        /// <summary>
        /// A random id the respondent's browser keeps for the survey, so the same browser can't answer
        /// twice without anyone needing an account. Null when the client didn't send one.
        /// </summary>
        public string? RespondentId { get; set; }

        /// <summary>
        /// Answers keyed by question key. Single-value questions hold one entry;
        /// checkbox and multiselect questions hold one entry per selected option.
        /// Questions that were hidden or left blank are absent.
        /// </summary>
        public Dictionary<string, List<string>> Answers { get; set; } = new();

        /// <summary>
        /// The points earned, when the survey was a quiz at the time of submitting; null otherwise.
        /// Scores are kept as graded: changing an answer key later doesn't regrade them.
        /// </summary>
        public decimal? Score { get; set; }

        /// <summary>The points that were available to this respondent, alongside <see cref="Score"/>.</summary>
        public decimal? MaxScore { get; set; }
    }
}
