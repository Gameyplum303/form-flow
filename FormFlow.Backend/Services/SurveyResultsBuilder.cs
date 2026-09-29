using System.Globalization;
using FormFlow.Data.Models;

namespace FormFlow.Backend.Services
{
    /// <summary>
    /// Turns a survey's stored responses into per-question summaries.
    /// </summary>
    public static class SurveyResultsBuilder
    {
        public const int RecentAnswerLimit = 5;

        public static SurveyResults Build(
            SurveyDefinition survey,
            IReadOnlyList<QuestionDefinition> questions,
            IReadOnlyList<SurveyResponse> responses)
        {
            return new SurveyResults
            {
                SurveyId = survey.Id,
                Title = survey.Title,
                TotalResponses = responses.Count,
                LastSubmittedAt = responses.Count == 0 ? null : responses.Max(r => r.SubmittedAt),
                Questions = questions.Select(q => Summarize(q, responses)).ToList()
            };
        }

        private static QuestionResult Summarize(QuestionDefinition question, IReadOnlyList<SurveyResponse> responses)
        {
            var answers = responses
                .Select(r => (r.SubmittedAt, Values: r.Answers.TryGetValue(question.Key, out var v) ? v : null))
                .Where(a => a.Values is { Count: > 0 })
                .Select(a => (a.SubmittedAt, Values: a.Values!))
                .ToList();

            var result = new QuestionResult
            {
                QuestionId = question.Id,
                Key = question.Key,
                Label = question.Label,
                Type = question.Type,
                AnsweredCount = answers.Count
            };

            var type = question.Type.ToLowerInvariant();
            var allValues = answers.SelectMany(a => a.Values).ToList();

            if (type == QuestionTypes.YesNo || (type == QuestionTypes.Checkbox && question.Options.Count == 0))
            {
                result.Options =
                [
                    Count("true", type == QuestionTypes.YesNo ? "Yes" : "Checked", allValues),
                    Count("false", type == QuestionTypes.YesNo ? "No" : "Not checked", allValues)
                ];
            }
            else if (QuestionTypes.IsChoice(type))
            {
                result.Options = question.Options.Select(o => Count(o.Value, o.Label, allValues)).ToList();
            }
            else if (type == QuestionTypes.Rating)
            {
                result.Options = Enumerable.Range(1, QuestionTypes.RatingScale(question))
                    .Select(n => Count(n.ToString(CultureInfo.InvariantCulture), n == 1 ? "1 star" : $"{n} stars", allValues))
                    .ToList();
                result.Numbers = Summarize(allValues);
            }
            else if (type == QuestionTypes.Number)
            {
                result.Numbers = Summarize(allValues);
            }
            else
            {
                result.RecentAnswers = answers
                    .OrderByDescending(a => a.SubmittedAt)
                    .Take(RecentAnswerLimit)
                    .Select(a => a.Values[0])
                    .ToList();
            }

            return result;
        }

        private static NumberSummary? Summarize(List<string> values)
        {
            var numbers = values
                .Select(v => decimal.TryParse(v, NumberStyles.Number, CultureInfo.InvariantCulture, out var n) ? n : (decimal?)null)
                .OfType<decimal>()
                .ToList();
            return numbers.Count == 0 ? null : new NumberSummary
            {
                Min = numbers.Min(),
                Max = numbers.Max(),
                Average = Math.Round(numbers.Average(), 2)
            };
        }

        private static OptionCount Count(string value, string label, List<string> values) => new()
        {
            Value = value,
            Label = label,
            Count = values.Count(v => v == value)
        };
    }
}
