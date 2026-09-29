using FluentAssertions;
using FormFlow.Backend.Services;
using FormFlow.Data.Models;

namespace FormFlow.Backend.Tests.Services
{
    public class SurveyResultsBuilderTests
    {
        private static readonly SurveyDefinition Survey = new() { Id = Guid.NewGuid(), Title = "Campus survey", Description = "", QuestionIds = [], CreatedAt = DateTime.UtcNow };

        private static readonly List<QuestionDefinition> Questions =
        [
            new() { Id = Guid.NewGuid(), Key = "is_student", Label = "Are you a student?", Type = QuestionTypes.YesNo },
            new()
            {
                Id = Guid.NewGuid(), Key = "campus", Label = "Campus", Type = QuestionTypes.Radio,
                Options = [new() { Value = "north", Label = "North" }, new() { Value = "south", Label = "South" }]
            },
            new()
            {
                Id = Guid.NewGuid(), Key = "skills", Label = "Skills", Type = QuestionTypes.Multiselect,
                Options = [new() { Value = "csharp", Label = "C#" }, new() { Value = "sql", Label = "SQL" }]
            },
            new() { Id = Guid.NewGuid(), Key = "age", Label = "Age", Type = QuestionTypes.Number },
            new() { Id = Guid.NewGuid(), Key = "stars", Label = "Rate it", Type = QuestionTypes.Rating },
        ];

        private static SurveyResponse Response(string submittedAt, bool? student, string? campus, int age, params string[] skills)
        {
            var answers = new Dictionary<string, List<string>> { ["age"] = [age.ToString()] };
            if (student is { } s)
            {
                answers["is_student"] = [s ? "true" : "false"];
            }
            if (campus is not null)
            {
                answers["campus"] = [campus];
            }
            if (skills.Length > 0)
            {
                answers["skills"] = skills.ToList();
            }
            return new SurveyResponse
            {
                Id = Guid.NewGuid(),
                SurveyId = Survey.Id,
                SubmittedAt = DateTime.Parse(submittedAt, null, System.Globalization.DateTimeStyles.AdjustToUniversal),
                Answers = answers
            };
        }

        private static readonly List<SurveyResponse> Responses =
        [
            Response("2026-09-01T10:00:00Z", true, "north", 20, "csharp", "sql"),
            Response("2026-09-01T23:30:00Z", true, "south", 22, "sql"),
            Response("2026-09-03T12:00:00Z", false, "north", 40, "csharp"),
            Response("2026-09-04T09:00:00Z", null, null, 35),
        ];

        private static readonly DateTime Now = new(2026, 9, 29, 12, 0, 0, DateTimeKind.Utc);

        private static SurveyResults Build(ResultsQuery? query = null, List<SurveyResponse>? responses = null) =>
            SurveyResultsBuilder.Build(Survey, Questions, responses ?? Responses, query, Now);

        private static QuestionResult Question(SurveyResults results, string key) => results.Questions.Single(q => q.Key == key);

        [Fact]
        public void WithoutAQuery_EveryResponseCounts()
        {
            var results = Build();

            results.TotalResponses.Should().Be(4);
            results.MatchingResponses.Should().Be(4);
            results.Comparison.Should().BeNull();
            Question(results, "age").Numbers!.Average.Should().Be(29.25m);
        }

        [Fact]
        public void Filters_KeepOnlyResponsesWithEveryFilteredAnswer()
        {
            var results = Build(new ResultsQuery { Filters = [new("is_student", "true"), new("skills", "csharp")] });

            results.TotalResponses.Should().Be(4);
            results.MatchingResponses.Should().Be(1);
            Question(results, "age").Numbers!.Average.Should().Be(20);
            Question(results, "campus").Options.Select(o => o.Count).Should().Equal(1, 0);
        }

        [Fact]
        public void DateRange_IncludesFrom_AndExcludesTo()
        {
            var results = Build(new ResultsQuery
            {
                From = new DateTime(2026, 9, 1, 23, 30, 0, DateTimeKind.Utc),
                To = new DateTime(2026, 9, 4, 9, 0, 0, DateTimeKind.Utc)
            });

            results.MatchingResponses.Should().Be(2);
            Question(results, "age").Numbers!.Average.Should().Be(31);
        }

        [Fact]
        public void Timeline_CountsEachDay_FromTheFirstResponseToTheLast_IncludingEmptyDays()
        {
            var results = Build();

            results.TimelineInterval.Should().Be(TimelineIntervals.Day);
            results.Timeline.Select(p => (p.Start, p.Count)).Should().Equal(
                ("2026-09-01", 2), ("2026-09-02", 0), ("2026-09-03", 1), ("2026-09-04", 1));
        }

        [Fact]
        public void Timeline_CountsLocalDays()
        {
            // 23:30 UTC on September 1 is 01:30 on September 2 two hours ahead of UTC.
            var results = Build(new ResultsQuery { UtcOffsetMinutes = 120 });

            results.Timeline.Select(p => (p.Start, p.Count)).Should().Equal(
                ("2026-09-01", 1), ("2026-09-02", 1), ("2026-09-03", 1), ("2026-09-04", 1));
        }

        [Fact]
        public void Timeline_CoversTheChosenRange_UpToToday()
        {
            var results = Build(new ResultsQuery { From = new DateTime(2026, 8, 30, 0, 0, 0, DateTimeKind.Utc), To = new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc) });

            results.Timeline.First().Start.Should().Be("2026-08-30");
            results.Timeline.Last().Start.Should().Be("2026-09-29", "the range hasn't ended yet");
            results.Timeline.Sum(p => p.Count).Should().Be(4);
        }

        [Fact]
        public void Timeline_WithNoResponses_IsEmpty()
        {
            Build(responses: []).Timeline.Should().BeEmpty();
        }

        [Fact]
        public void LongTimelines_CountWeeksThatStartOnMonday_ThenMonths()
        {
            var spread = new List<SurveyResponse>
            {
                Response("2026-01-07T12:00:00Z", true, "north", 20),
                Response("2026-01-09T12:00:00Z", true, "north", 20),
                Response("2026-06-01T12:00:00Z", true, "north", 20),
            };

            var weekly = Build(responses: spread);
            weekly.TimelineInterval.Should().Be(TimelineIntervals.Week);
            weekly.Timeline.First().Should().BeEquivalentTo(new TimelinePoint { Start = "2026-01-05", Count = 2 });
            weekly.Timeline.Last().Should().BeEquivalentTo(new TimelinePoint { Start = "2026-06-01", Count = 1 });

            spread.Add(Response("2028-03-15T12:00:00Z", true, "north", 20));
            var monthly = Build(responses: spread);
            monthly.TimelineInterval.Should().Be(TimelineIntervals.Month);
            monthly.Timeline.First().Should().BeEquivalentTo(new TimelinePoint { Start = "2026-01-01", Count = 2 });
            monthly.Timeline.Should().HaveCount(27);
        }

        [Fact]
        public void Timeline_NeverReachesFurtherBackThanTwentyYears()
        {
            var results = Build(new ResultsQuery { From = new DateTime(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc) });

            results.TimelineInterval.Should().Be(TimelineIntervals.Month);
            results.Timeline.Should().HaveCount(20 * 12 + 1);
        }

        [Fact]
        public void Comparison_SummarizesEachAnswerGroup_AndThoseWhoDidNotAnswer()
        {
            var results = Build(new ResultsQuery { CompareBy = "is_student" });

            var comparison = results.Comparison!;
            comparison.Label.Should().Be("Are you a student?");
            comparison.Groups.Select(g => (g.Label, g.Responses)).Should().Equal(("Yes", 2), ("No", 1), ("No answer", 1));
            comparison.Groups[0].Questions.Single(q => q.Key == "age").Numbers!.Average.Should().Be(21);
            comparison.Groups[1].Questions.Single(q => q.Key == "age").Numbers!.Average.Should().Be(40);
            comparison.Groups[0].Questions.Select(q => q.Key).Should().Equal(results.Questions.Select(q => q.Key));
        }

        [Fact]
        public void Comparison_ByAMultiSelectQuestion_CountsAResponseInEveryGroupItChose()
        {
            var results = Build(new ResultsQuery { CompareBy = "skills", Filters = [new("is_student", "true")] });

            results.Comparison!.Groups.Select(g => (g.Value, g.Responses)).Should().Equal(("csharp", 1), ("sql", 2));
        }

        [Fact]
        public void FixedAnswers_OnlyExistForQuestionsWithASetOfAnswers()
        {
            SurveyResultsBuilder.FixedAnswers(Questions[4])!.Select(a => a.Label).Should().Equal("1 star", "2 stars", "3 stars", "4 stars", "5 stars");
            SurveyResultsBuilder.FixedAnswers(Questions[3]).Should().BeNull();
            SurveyResultsBuilder.FixedAnswers(new QuestionDefinition { Key = "agree", Label = "I agree", Type = QuestionTypes.Checkbox })!
                .Select(a => a.Label).Should().Equal("Checked", "Not checked");
        }
    }
}
