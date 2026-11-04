using FluentAssertions;
using FormFlow.Backend.Services;
using FormFlow.Data.Models;

namespace FormFlow.Backend.Tests.Services
{
    /// <summary>The quiz part of <see cref="SurveyResultsBuilder"/>, and the score columns of <see cref="CsvExporter"/>.</summary>
    public class QuizSummaryTests
    {
        private static readonly QuestionDefinition Capital = new()
        {
            Id = Guid.NewGuid(),
            Key = "capital",
            Label = "Capital of France",
            Type = QuestionTypes.Radio,
            Options = [new() { Value = "paris", Label = "Paris" }, new() { Value = "lyon", Label = "Lyon" }],
            AnswerKey = new AnswerKey { CorrectAnswers = ["paris"], Points = 10 },
        };

        private static readonly QuestionDefinition Opinion = new() { Id = Guid.NewGuid(), Key = "opinion", Label = "Liked it?", Type = QuestionTypes.YesNo };

        private static readonly List<QuestionDefinition> Questions = [Capital, Opinion];

        private static SurveyResponse Response(decimal? score, decimal? max, string capital = "paris") => new()
        {
            Id = Guid.NewGuid(),
            SubmittedAt = new DateTime(2026, 10, 1, 9, 0, 0, DateTimeKind.Utc),
            Answers = new() { ["capital"] = [capital] },
            Score = score,
            MaxScore = max,
        };

        [Fact]
        public void Distribution_PutsEachScoreInItsTenth()
        {
            var responses = new[] { 0m, 9m, 10m, 89m, 90m, 100m }.Select(s => Response(s, 100)).ToList();

            var summary = SurveyResultsBuilder.SummarizeQuiz(Questions, responses);

            summary.Distribution.Select(b => b.Label).Should().Equal(
                "0–9%", "10–19%", "20–29%", "30–39%", "40–49%", "50–59%", "60–69%", "70–79%", "80–89%", "90–100%");
            summary.Distribution.Select(b => b.Count).Should().Equal(2, 1, 0, 0, 0, 0, 0, 0, 1, 2);
            summary.Distribution[^1].Should().Match<ScoreBand>(b => b.MinPercent == 90 && b.MaxPercent == 100);
        }

        [Fact]
        public void ResponsesWithoutAScore_OrWithNothingToGrade_AreLeftOut()
        {
            var summary = SurveyResultsBuilder.SummarizeQuiz(Questions,
                [Response(10, 10), Response(null, null, "lyon"), Response(0, 0, "lyon")]);

            summary.GradedResponses.Should().Be(1);
            summary.AverageScore.Should().Be(10);
            summary.AveragePercent.Should().Be(100);
            summary.Questions.Should().ContainSingle().Which.Should().Match<QuizQuestionSummary>(q =>
                q.Key == "capital" && q.Points == 10 && q.Graded == 1 && q.Correct == 1 && q.PercentCorrect == 100);
        }

        [Fact]
        public void Averages_RoundHalvesUp()
        {
            // (5.33 + 7) / 2 = 6.165, which banker's rounding would make 6.16.
            var summary = SurveyResultsBuilder.SummarizeQuiz(Questions, [Response(5.33m, 7), Response(7, 7)]);

            summary.AverageScore.Should().Be(6.17m);
            summary.AverageMaxScore.Should().Be(7);
            summary.AveragePercent.Should().Be(88.1m);
        }

        [Fact]
        public void NoGradedResponses_GiveAnEmptySummary()
        {
            var summary = SurveyResultsBuilder.SummarizeQuiz(Questions, []);

            summary.GradedResponses.Should().Be(0);
            summary.AverageScore.Should().Be(0);
            summary.AveragePercent.Should().Be(0);
            summary.Distribution.Should().OnlyContain(b => b.Count == 0);
            summary.Questions.Single().Should().Match<QuizQuestionSummary>(q => q.Graded == 0 && q.PercentCorrect == 0 && q.AveragePoints == 0);
        }

        [Fact]
        public void Build_OnlySummarizesQuizzes()
        {
            var survey = new SurveyDefinition { Id = Guid.NewGuid(), Title = "Quiz", Description = "", QuestionIds = [], CreatedAt = DateTime.UtcNow };
            List<SurveyResponse> responses = [Response(10, 10)];

            SurveyResultsBuilder.Build(survey, Questions, responses).Quiz.Should().BeNull();
            survey.IsQuiz = true;
            SurveyResultsBuilder.Build(survey, Questions, responses).Quiz!.GradedResponses.Should().Be(1);
        }

        [Fact]
        public void Csv_WithScores_AddsScoreAndMaxScoreColumns()
        {
            var csv = CsvExporter.Export(Questions, [Response(7.5m, 10), Response(null, null)], includeScores: true);
            var lines = csv.Split("\r\n");

            lines[0].Should().Be("response_id,submitted_at,submitted_by,score,max_score,capital,opinion");
            lines[1].Split(',')[3..5].Should().Equal("7.5", "10");
            lines[2].Split(',')[3..5].Should().Equal("", "");
            CsvExporter.Export(Questions, [Response(7.5m, 10)]).Should().StartWith("response_id,submitted_at,submitted_by,capital,");
        }
    }
}
