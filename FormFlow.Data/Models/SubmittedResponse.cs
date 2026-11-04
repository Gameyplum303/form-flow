namespace FormFlow.Data.Models
{
    /// <summary>
    /// The 201 body of POST /api/surveys/{id}/responses: the stored response, plus the respondent's grade
    /// when the survey is a quiz.
    /// </summary>
    public class SubmittedResponse : SurveyResponse
    {
        /// <summary>The grade, for a quiz; null otherwise.</summary>
        public QuizResult? Quiz { get; set; }

        public static SubmittedResponse From(SurveyResponse response, QuizResult? quiz) => new()
        {
            Id = response.Id,
            SurveyId = response.SurveyId,
            SubmittedAt = response.SubmittedAt,
            SubmittedBy = response.SubmittedBy,
            RespondentId = response.RespondentId,
            Answers = response.Answers,
            Score = response.Score,
            MaxScore = response.MaxScore,
            Quiz = quiz,
        };
    }
}
