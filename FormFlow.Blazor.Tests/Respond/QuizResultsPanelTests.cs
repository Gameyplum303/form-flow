using Bunit;
using FluentAssertions;
using FormFlow.Blazor.Components.Pages.Admin;
using FormFlow.Blazor.Services;
using FormFlow.Data.Models;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;

namespace FormFlow.Blazor.Tests.Respond;

/// <summary>The quiz summary on the results page.</summary>
public class QuizResultsPanelTests
{
    private readonly FakeSurveyService _service = new();
    private readonly Guid _id = Guid.NewGuid();

    private BunitContext CreateContext()
    {
        var ctx = new BunitContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        ctx.Services.AddMudServices();
        ctx.Services.AddSingleton<ISurveyService>(_service);
        return ctx;
    }

    private SurveyResults Results(QuizSummary? quiz) => new()
    {
        SurveyId = _id,
        Title = "Geography quiz",
        TotalResponses = 4,
        MatchingResponses = 4,
        Questions = [new QuestionResult { Key = "capital", Label = "Capital of France?", Type = "radio", AnsweredCount = 4 }],
        Quiz = quiz,
    };

    private static List<ScoreBand> Bands(params int[] counts) =>
        Enumerable.Range(0, 10).Select(i => new ScoreBand
        {
            MinPercent = i * 10,
            MaxPercent = i == 9 ? 100 : i * 10 + 9,
            Label = i == 9 ? "90–100%" : $"{i * 10}–{i * 10 + 9}%",
            Count = counts.ElementAtOrDefault(i),
        }).ToList();

    [Fact]
    public async Task Shows_the_average_the_distribution_and_each_questions_share_of_correct_answers()
    {
        _service.Results = Results(new QuizSummary
        {
            GradedResponses = 4,
            AverageScore = 7.5m,
            AverageMaxScore = 10,
            AveragePercent = 75,
            Distribution = Bands(0, 0, 0, 0, 0, 1, 0, 1, 0, 2),
            Questions =
            [
                new QuizQuestionSummary { Key = "capital", Label = "Capital of France?", Points = 2, Graded = 4, Correct = 3, PercentCorrect = 75, AveragePoints = 1.5m },
                new QuizQuestionSummary { Key = "sum", Label = "What is 2 + 2?", Points = 3, Graded = 0 },
            ],
        });
        await using var ctx = CreateContext();

        var cut = ctx.Render<AdminSurveyResults>(p => p.Add(x => x.Id, _id));

        cut.WaitForAssertion(() => cut.FindAll("[data-quiz-summary]").Should().ContainSingle());
        cut.Find("[data-quiz-average]").TextContent.Trim().Should().Be("7.5 of 10 points");
        cut.Find("[data-quiz-average-percent]").TextContent.Trim().Should().Be("75%");
        cut.FindAll("[data-score-band]").Should().HaveCount(10);
        cut.Find("[data-score-band='90–100%']").TextContent.Should().Contain("2 (50%)");
        var rows = cut.FindAll("[data-quiz-question]");
        rows[0].TextContent.Should().Contain("3 of 4 (75%)").And.Contain("1.5");
        rows[1].TextContent.Should().Contain("–");
    }

    [Fact]
    public async Task Says_so_when_no_response_is_graded()
    {
        _service.Results = Results(new QuizSummary { Distribution = Bands(), Questions = [] });
        await using var ctx = CreateContext();

        var cut = ctx.Render<AdminSurveyResults>(p => p.Add(x => x.Id, _id));

        cut.WaitForAssertion(() => cut.Find("[data-quiz-empty]").TextContent.Should().Contain("No graded responses yet."));
        cut.FindAll("[data-score-band]").Should().BeEmpty();
    }

    [Fact]
    public async Task Surveys_that_arent_quizzes_have_no_quiz_summary()
    {
        _service.Results = Results(null);
        await using var ctx = CreateContext();

        var cut = ctx.Render<AdminSurveyResults>(p => p.Add(x => x.Id, _id));

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Capital of France?"));
        cut.FindAll("[data-quiz-summary]").Should().BeEmpty();
    }

    [Theory]
    [InlineData(8, 10, "8 of 10 points")]
    [InlineData(1, 1, "1 of 1 point")]
    [InlineData(0.33, 2.5, "0.33 of 2.5 points")]
    public void QuizText_WritesScoresWithoutTrailingZeros(decimal score, decimal max, string text)
    {
        QuizText.Score(score, max).Should().Be(text);
    }
}
