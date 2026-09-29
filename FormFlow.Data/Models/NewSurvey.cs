namespace FormFlow.Data.Models
{
    /// <summary>Body of POST and PUT /api/surveys: what a survey builder sends.</summary>
    public class NewSurvey
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<Guid> QuestionIds { get; set; } = [];

        /// <summary>The ids of the questions that start a new page. Ids that aren't in the survey are ignored.</summary>
        public List<Guid> PageBreaks { get; set; } = [];
    }
}