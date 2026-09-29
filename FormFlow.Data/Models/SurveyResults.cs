namespace FormFlow.Data.Models
{
    /// <summary>
    /// Aggregated answers for a survey, as shown on the admin results page.
    /// </summary>
    public class SurveyResults
    {
        public Guid SurveyId { get; set; }
        public string Title { get; set; } = string.Empty;
        /// <summary>Every response to the survey, before any filter.</summary>
        public int TotalResponses { get; set; }

        /// <summary>The responses that match the filters and dates; the summaries below count only these.</summary>
        public int MatchingResponses { get; set; }

        public DateTime? LastSubmittedAt { get; set; }
        public List<QuestionResult> Questions { get; set; } = new();

        /// <summary>"day", "week" or "month": how much time each <see cref="Timeline"/> point covers.</summary>
        public string TimelineInterval { get; set; } = TimelineIntervals.Day;

        /// <summary>Matching responses per day (or week or month), oldest first, with empty periods included.</summary>
        public List<TimelinePoint> Timeline { get; set; } = new();

        /// <summary>The same summaries split by the answer to one question, when a comparison was asked for.</summary>
        public ResultComparison? Comparison { get; set; }
    }

    public static class TimelineIntervals
    {
        public const string Day = "day";
        public const string Week = "week";
        public const string Month = "month";
    }

    public class TimelinePoint
    {
        /// <summary>The first day of the period, as yyyy-MM-dd in the requested time zone.</summary>
        public string Start { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    public class ResultComparison
    {
        public string Key { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;

        /// <summary>One group per answer, then "No answer" when some responses skipped the question.</summary>
        public List<ResultGroup> Groups { get; set; } = new();
    }

    public class ResultGroup
    {
        /// <summary>The answer this group had, or an empty string for the "No answer" group.</summary>
        public string Value { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public int Responses { get; set; }

        /// <summary>Summaries of this group's responses, in the same order as <see cref="SurveyResults.Questions"/>.</summary>
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
