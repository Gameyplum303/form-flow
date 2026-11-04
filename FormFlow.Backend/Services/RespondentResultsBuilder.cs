using FormFlow.Data.Models;

namespace FormFlow.Backend.Services
{
    /// <summary>
    /// Builds the summary respondents see after answering (<see cref="RespondentResults"/>). Only questions
    /// with a fixed set of answers are included: yes/no, dropdown, radio, checkbox, multiselect, rating
    /// and NPS. Text, email, number, date, slider and likert answers are left out, so nothing a respondent
    /// typed, and no answer that could single someone out, reaches other respondents.
    /// </summary>
    public static class RespondentResultsBuilder
    {
        /// <summary>Whether a question's answers appear in the respondents' summary.</summary>
        public static bool IsIncluded(QuestionDefinition question) => SurveyResultsBuilder.FixedAnswers(question) is not null;

        public static RespondentResults Build(
            SurveyDefinition survey,
            IReadOnlyList<QuestionDefinition> questions,
            IReadOnlyList<SurveyResponse> responses)
        {
            var included = questions.Where(IsIncluded).ToList();
            var results = SurveyResultsBuilder.Build(survey, included, responses);

            return new RespondentResults
            {
                SurveyId = survey.Id,
                Title = survey.Title,
                TotalResponses = results.TotalResponses,
                // The summaries come back in the order of the questions they were built from.
                Questions = included.Zip(results.Questions, (question, q) => new RespondentQuestionResult
                {
                    Key = q.Key,
                    Label = q.Label,
                    Type = q.Type,
                    AnsweredCount = q.AnsweredCount,
                    MultipleAnswers = QuestionTypes.IsMultiValue(question.Type) && question.Options.Count > 0,
                    Options = q.Options.Select(o => new RespondentOptionResult
                    {
                        Value = o.Value,
                        Label = o.Label,
                        Count = o.Count,
                        Percent = Percent(o.Count, q.AnsweredCount),
                    }).ToList(),
                }).ToList(),
            };
        }

        private static int Percent(int count, int answered) =>
            answered == 0 ? 0 : (int)Math.Round(count * 100m / answered, MidpointRounding.AwayFromZero);
    }
}
