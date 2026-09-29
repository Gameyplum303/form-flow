using System.Globalization;
using FormFlow.Data.Models;
using Microsoft.AspNetCore.Mvc;

namespace FormFlow.Backend.Services
{
    /// <summary>The query parameters the results and CSV export endpoints accept.</summary>
    /// <param name="Filter">Repeatable <c>key:value</c> pairs, for example <c>is_student:true</c>.</param>
    /// <param name="From">Only responses sent at or after this ISO 8601 time.</param>
    /// <param name="To">Only responses sent before this ISO 8601 time.</param>
    /// <param name="CompareBy">The key of a question to split the results by.</param>
    /// <param name="UtcOffset">Minutes ahead of UTC, so the timeline counts local days.</param>
    public record ResultsParameters(
        [FromQuery] string[]? Filter,
        [FromQuery] string? From,
        [FromQuery] string? To,
        [FromQuery] string? CompareBy,
        [FromQuery] string? UtcOffset);

    /// <summary>Reads <see cref="ResultsParameters"/> into a <see cref="ResultsQuery"/>, checking them against the survey's questions.</summary>
    public static class ResultsQueryParser
    {
        public const int MaxFilters = 10;
        public const int MaxUtcOffsetMinutes = 14 * 60;

        public static ResultsQuery Parse(ResultsParameters parameters, IReadOnlyList<QuestionDefinition> questions,
            out Dictionary<string, string[]> errors)
        {
            var problems = new Dictionary<string, List<string>>();
            void Add(string field, string message)
            {
                if (!problems.TryGetValue(field, out var list))
                {
                    problems[field] = list = new List<string>();
                }
                list.Add(message);
            }

            var query = new ResultsQuery();
            var filters = parameters.Filter ?? [];
            if (filters.Length > MaxFilters)
            {
                Add("filter", $"Use at most {MaxFilters} filters.");
            }
            foreach (var filter in filters.Take(MaxFilters))
            {
                var colon = filter.IndexOf(':');
                if (colon <= 0)
                {
                    Add("filter", $"'{filter}' should be key:value, for example is_student:true.");
                    continue;
                }
                var key = filter[..colon];
                var value = filter[(colon + 1)..];
                if (FixedAnswersOf(key, questions, out var message, "filter") is not { } answers)
                {
                    Add("filter", message!);
                }
                else if (!answers.Any(a => a.Value == value))
                {
                    Add("filter", $"'{value}' isn't one of the answers to '{key}'.");
                }
                else
                {
                    query.Filters.Add(new AnswerFilter(key, value));
                }
            }

            query.From = ParseTime(parameters.From, "from", Add);
            query.To = ParseTime(parameters.To, "to", Add);
            if (query.From is { } from && query.To is { } to && from >= to)
            {
                Add("to", "'to' must be after 'from'.");
            }

            if (!string.IsNullOrWhiteSpace(parameters.CompareBy))
            {
                if (FixedAnswersOf(parameters.CompareBy, questions, out var message, "split results") is null)
                {
                    Add("compareBy", message!);
                }
                else
                {
                    query.CompareBy = parameters.CompareBy;
                }
            }

            if (!string.IsNullOrWhiteSpace(parameters.UtcOffset))
            {
                if (int.TryParse(parameters.UtcOffset, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var offset) &&
                    Math.Abs(offset) <= MaxUtcOffsetMinutes)
                {
                    query.UtcOffsetMinutes = offset;
                }
                else
                {
                    Add("utcOffset", $"utcOffset must be a whole number of minutes between -{MaxUtcOffsetMinutes} and {MaxUtcOffsetMinutes}.");
                }
            }

            errors = problems.ToDictionary(p => p.Key, p => p.Value.ToArray());
            return query;
        }

        private static IReadOnlyList<(string Value, string Label)>? FixedAnswersOf(string key,
            IReadOnlyList<QuestionDefinition> questions, out string? message, string purpose)
        {
            message = null;
            var question = questions.FirstOrDefault(q => q.Key == key);
            if (question is null)
            {
                message = $"This survey has no question with the key '{key}'.";
                return null;
            }
            var answers = SurveyResultsBuilder.FixedAnswers(question);
            if (answers is null)
            {
                message = $"Only yes/no, choice and rating questions can {purpose}; '{key}' is a {question.Type} question.";
            }
            return answers;
        }

        private static DateTime? ParseTime(string? value, string field, Action<string, string> add)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }
            if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var time))
            {
                return time.UtcDateTime;
            }
            add(field, $"'{field}' must be an ISO 8601 date or time, for example 2026-09-29T04:00:00Z.");
            return null;
        }
    }
}
