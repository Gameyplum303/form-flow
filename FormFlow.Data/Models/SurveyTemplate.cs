namespace FormFlow.Data.Models
{
    /// <summary>A ready-made survey in GET /api/templates, which builders start new surveys from.</summary>
    public class SurveyTemplate
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int QuestionCount { get; set; }

        /// <summary>How many pages the survey has: one more than its page breaks.</summary>
        public int PageCount { get; set; } = 1;
    }
}
