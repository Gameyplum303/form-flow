namespace FormFlow.Data.Models
{
    /// <summary>
    /// Aggregated answers for a survey, as shown on the admin results page.
    /// </summary>
    public class SurveyResults
    {
        public Guid SurveyId { get; set; }
        public string Title { get; set; } = string.Empty;
        public int TotalResponses { get; set; }
        public DateTime? LastSubmittedAt { get; set; }
        public List<QuestionResult> Questions { get; set; } = new();
    }

    public class QuestionResult
    {
        public Guid QuestionId { get; set; }
        public string Key { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;

        /// <summary>How many responses answered this question.</summary>
        public int AnsweredCount { get; set; }

        /// <summary>Counts per option, for choice and yes/no questions.</summary>
        public List<OptionCount> Options { get; set; } = new();

        /// <summary>Min, max and average, for number questions with at least one answer.</summary>
        public NumberSummary? Numbers { get; set; }

        /// <summary>The most recent answers, newest first, for text questions.</summary>
        public List<string> RecentAnswers { get; set; } = new();
    }

    public class OptionCount
    {
        public string Value { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    public class NumberSummary
    {
        public decimal Min { get; set; }
        public decimal Max { get; set; }
        public decimal Average { get; set; }
    }
}
