using System.Globalization;
using FormFlow.Data.Models;

namespace FormFlow.Backend.Services
{
    /// <summary>
    /// Turns a survey's stored responses into per-question summaries, optionally narrowed by a
    /// <see cref="ResultsQuery"/>, with a timeline of when they arrived and a comparison of groups.
    /// </summary>
    public static class SurveyResultsBuilder
    {
        public const int RecentAnswerLimit = 5;
        public const string NoAnswerLabel = "No answer";

        /// <summary>Longer timelines count weeks, and longer still, months.</summary>
        public const int MaxDailyTimelineDays = 90;
        public const int MaxWeeklyTimelineDays = 730;

        /// <summary>How far back a timeline goes at most, so an early "from" date can't make it huge.</summary>
        public const int MaxTimelineYears = 20;

        public static SurveyResults Build(
            SurveyDefinition survey,
            IReadOnlyList<QuestionDefinition> questions,
            IReadOnlyList<SurveyResponse> responses,
            ResultsQuery? query = null,
            DateTime? now = null)
        {
            query ??= new ResultsQuery();
            var matching = Filter(responses, query).ToList();
            var (interval, timeline) = BuildTimeline(matching, query, now ?? DateTime.UtcNow);

            return new SurveyResults
            {
                SurveyId = survey.Id,
                Title = survey.Title,
                TotalResponses = responses.Count,
                MatchingResponses = matching.Count,
                LastSubmittedAt = responses.Count == 0 ? null : responses.Max(r => r.SubmittedAt),
                Questions = questions.Select(q => Summarize(q, matching)).ToList(),
                TimelineInterval = interval,
                Timeline = timeline,
                Comparison = Compare(query.CompareBy, questions, matching)
            };
        }

        /// <summary>
        /// The answers a question can have, with the labels results show for them, or null for
        /// questions answered in free text, numbers or dates. Only these questions can filter or
        /// split results.
        /// </summary>
        public static IReadOnlyList<(string Value, string Label)>? FixedAnswers(QuestionDefinition question)
        {
            var type = question.Type.ToLowerInvariant();
            if (type == QuestionTypes.YesNo || (type == QuestionTypes.Checkbox && question.Options.Count == 0))
            {
                return type == QuestionTypes.YesNo
                    ? [("true", "Yes"), ("false", "No")]
                    : [("true", "Checked"), ("false", "Not checked")];
            }
            if (QuestionTypes.IsChoice(type))
            {
                return question.Options.Select(o => (o.Value, o.Label)).ToList();
            }
            if (type == QuestionTypes.Rating)
            {
                return Enumerable.Range(1, QuestionTypes.RatingScale(question))
                    .Select(n => (n.ToString(CultureInfo.InvariantCulture), n == 1 ? "1 star" : $"{n} stars"))
                    .ToList();
            }
            return null;
        }

        /// <summary>The responses sent in the query's date range that gave every filtered answer.</summary>
        public static IEnumerable<SurveyResponse> Filter(IEnumerable<SurveyResponse> responses, ResultsQuery query) =>
            responses.Where(r =>
                (query.From is not { } from || Utc(r.SubmittedAt) >= from) &&
                (query.To is not { } to || Utc(r.SubmittedAt) < to) &&
                query.Filters.All(f => Answered(r, f.Key, f.Value)));

        private static bool Answered(SurveyResponse response, string key, string value) =>
            response.Answers.TryGetValue(key, out var values) && values.Contains(value);

        // LiteDB hands dates back in local time.
        private static DateTime Utc(DateTime time) => time.Kind == DateTimeKind.Local
            ? time.ToUniversalTime()
            : DateTime.SpecifyKind(time, DateTimeKind.Utc);

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

            if (FixedAnswers(question) is { } fixedAnswers)
            {
                result.Options = fixedAnswers.Select(a => Count(a.Value, a.Label, allValues)).ToList();
                if (type == QuestionTypes.Rating)
                {
                    result.Numbers = Summarize(allValues);
                }
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

        /// <summary>
        /// Counts responses per local day (or week or month) from the first response, or the
        /// "from" date, to the last response, or the "to" date if it has passed, or today.
        /// </summary>
        private static (string Interval, List<TimelinePoint> Points) BuildTimeline(
            List<SurveyResponse> matching, ResultsQuery query, DateTime now)
        {
            var offset = TimeSpan.FromMinutes(query.UtcOffsetMinutes);
            DateOnly Local(DateTime utc) => DateOnly.FromDateTime(Utc(utc) + offset);

            var days = matching.Select(r => Local(r.SubmittedAt)).ToList();
            var today = Local(now);
            DateOnly? first = query.From is { } from ? Local(from) : days.Count > 0 ? days.Min() : null;
            DateOnly? last = days.Count > 0 ? days.Max() : null;
            if (query.From is not null || query.To is not null)
            {
                var end = query.To is { } to && Local(to.AddTicks(-1)) < today ? Local(to.AddTicks(-1)) : today;
                last = last is { } l && l > end ? l : end;
            }
            if (first is not { } start || last is not { } stop || start > stop)
            {
                return (TimelineIntervals.Day, []);
            }
            if (start < stop.AddYears(-MaxTimelineYears))
            {
                start = stop.AddYears(-MaxTimelineYears);
            }

            var span = stop.DayNumber - start.DayNumber;
            var interval = span > MaxWeeklyTimelineDays ? TimelineIntervals.Month
                : span > MaxDailyTimelineDays ? TimelineIntervals.Week
                : TimelineIntervals.Day;
            DateOnly PeriodOf(DateOnly day) => interval switch
            {
                TimelineIntervals.Month => new DateOnly(day.Year, day.Month, 1),
                TimelineIntervals.Week => day.AddDays(-(((int)day.DayOfWeek + 6) % 7)),
                _ => day,
            };

            var counts = days.GroupBy(PeriodOf).ToDictionary(g => g.Key, g => g.Count());
            var points = new List<TimelinePoint>();
            for (var period = PeriodOf(start); period <= stop;
                 period = interval == TimelineIntervals.Month ? period.AddMonths(1) : period.AddDays(interval == TimelineIntervals.Week ? 7 : 1))
            {
                points.Add(new TimelinePoint
                {
                    Start = period.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    Count = counts.GetValueOrDefault(period)
                });
            }
            return (interval, points);
        }

        private static ResultComparison? Compare(string? key, IReadOnlyList<QuestionDefinition> questions, List<SurveyResponse> matching)
        {
            if (string.IsNullOrEmpty(key) || questions.FirstOrDefault(q => q.Key == key) is not { } question ||
                FixedAnswers(question) is not { } answers)
            {
                return null;
            }

            var groups = answers
                .Select(a => (a.Value, a.Label, Responses: matching.Where(r => Answered(r, key, a.Value)).ToList()))
                .ToList();
            var unanswered = matching.Where(r => !r.Answers.TryGetValue(key, out var v) || v.Count == 0).ToList();
            if (unanswered.Count > 0)
            {
                groups.Add((string.Empty, NoAnswerLabel, unanswered));
            }

            return new ResultComparison
            {
                Key = question.Key,
                Label = question.Label,
                Groups = groups.Select(g => new ResultGroup
                {
                    Value = g.Value,
                    Label = g.Label,
                    Responses = g.Responses.Count,
                    Questions = questions.Select(q => Summarize(q, g.Responses)).ToList()
                }).ToList()
            };
        }
    }
}
