namespace FormFlow.Data.Models
{
    /// <summary>Body of POST and PUT /api/surveys: what a survey builder sends.</summary>
    public class NewSurvey
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<Guid> QuestionIds { get; set; } = [];
    }
}