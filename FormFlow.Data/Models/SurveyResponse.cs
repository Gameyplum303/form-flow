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
        /// Answers keyed by question key. Single-value questions hold one entry;
        /// checkbox and multiselect questions hold one entry per selected option.
        /// Questions that were hidden or left blank are absent.
        /// </summary>
        public Dictionary<string, List<string>> Answers { get; set; } = new();
    }
}
