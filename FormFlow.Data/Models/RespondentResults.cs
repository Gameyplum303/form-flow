namespace FormFlow.Data.Models
{
    /// <summary>
    /// The read-only summary respondents see after answering a survey that shows its results
    /// (<see cref="SurveyDefinition.ShowResultsToRespondents"/>). It holds counts for the questions with a
    /// fixed set of answers only: never free text, numbers, dates, respondent ids or account names.
    /// </summary>
    public class RespondentResults
    {
        public Guid SurveyId { get; set; }
        public string Title { get; set; } = string.Empty;
        public int TotalResponses { get; set; }

        /// <summary>The yes/no, choice, rating and NPS questions, in survey order.</summary>
        public List<RespondentQuestionResult> Questions { get; set; } = new();
    }

    public class RespondentQuestionResult
    {
        public string Key { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;

        /// <summary>How many responses answered this question; the percentages are of these.</summary>
        public int AnsweredCount { get; set; }

        /// <summary>Whether a respondent could pick more than one option, so the percentages can add up to more than 100.</summary>
        public bool MultipleAnswers { get; set; }

        public List<RespondentOptionResult> Options { get; set; } = new();
    }

    public class RespondentOptionResult
    {
        public string Value { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public int Count { get; set; }

        /// <summary>The share of the question's answers that picked this option, rounded to a whole percent.</summary>
        public int Percent { get; set; }
    }
}
