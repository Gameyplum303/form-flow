using Bunit;
using FluentAssertions;
using FormFlow.Blazor.Components.Pages.Respond;
using FormFlow.Blazor.Components.Shared;
using FormFlow.Blazor.Services;
using FormFlow.Data.Models;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;

namespace FormFlow.Blazor.Tests.Respond;

/// <summary>Taking a quiz: the notice before submitting, then the score and the review on the thank-you screen.</summary>
public class QuizTakeSurveyTests
{
    private readonly FakeSurveyService _service = new();
    private readonly SurveyDefinition _survey;

    private static readonly QuizResult Graded = new()
    {
        Score = 1.5m,
        MaxScore = 3,
        Percent = 50,
        Questions =
        [
            new QuestionGrade
            {
                Key = "capital", Label = "Capital of France?", Points = 1, MaxPoints = 1, Correct = true,
                GivenAnswers = ["Paris"], CorrectAnswers = ["Paris"], Explanation = "Since the tenth century.",
            },
            new QuestionGrade
            {
                Key = "primes", Label = "Which are prime?", Points = 0.5m, MaxPoints = 1, Correct = false,
                GivenAnswers = ["Two"], CorrectAnswers = ["Two", "Three"],
            },
            new QuestionGrade
            {
                Key = "sum", Label = "What is 2 + 2?", Points = 0, MaxPoints = 1, Correct = false,
                GivenAnswers = [], CorrectAnswers = ["4"],
            },
        ],
    };

    public QuizTakeSurveyTests()
    {
        var capital = new QuestionDefinition
        {
            Id = Guid.NewGuid(),
            Key = "capital",
            Label = "Capital of France?",
            Type = "text",
            Required = false,
        };
        _survey = new SurveyDefinition
        {
            Id = Guid.NewGuid(),
            Title = "Geography quiz",
            Description = "No notes",
            QuestionIds = [capital.Id],
            CreatedAt = DateTime.UtcNow,
            Status = SurveyStatuses.Published,
            IsQuiz = true,
        };
        _service.Surveys.Add(_survey);
        _service.Questions[_survey.Id] = [capital];
    }

    private BunitContext CreateContext()
    {
        var ctx = new BunitContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        ctx.Services.AddMudServices();
        ctx.Services.AddSingleton<ISurveyService>(_service);
        ctx.Services.AddSingleton<IRespondentIdentity>(new FakeRespondentIdentity());
        ctx.Services.AddSingleton<ISurveyDrafts>(new FakeSurveyDrafts());
        ctx.Render<MudPopoverProvider>();
        return ctx;
    }

    private async Task<IRenderedComponent<TakeSurvey>> SubmitAsync(BunitContext ctx)
    {
        var cut = ctx.Render<TakeSurvey>(p => p.Add(x => x.Id, _survey.Id));
        cut.WaitForAssertion(() => cut.Markup.Should().Contain(_survey.Title));
        await cut.InvokeAsync(() => cut.FindAll("button").Single(b => b.TextContent.Contains("Submit")).Click());
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Thank you!"));
        return cut;
    }

    [Fact]
    public async Task A_quiz_says_so_before_submitting()
    {
        await using var ctx = CreateContext();
        var cut = ctx.Render<TakeSurvey>(p => p.Add(x => x.Id, _survey.Id));

        cut.WaitForAssertion(() => cut.Find("[data-quiz-notice]").TextContent.Should().Contain("This is a quiz."));
    }

    [Fact]
    public async Task The_thank_you_screen_shows_the_score_and_the_review()
    {
        _service.NextSubmitResult = new SubmitResult(true, new Dictionary<string, string[]>(), Quiz: Graded);
        await using var ctx = CreateContext();

        var cut = await SubmitAsync(ctx);

        cut.Find("[data-quiz-score]").TextContent.Should().Be("1.5 of 3 points");
        cut.Find("[data-quiz-percent]").TextContent.Should().Be("50%");
        var questions = cut.FindAll("[data-quiz-question]");
        questions.Select(q => q.GetAttribute("data-correct")).Should().Equal("true", "false", "false");
        questions.Select(q => q.QuerySelector("[data-quiz-verdict]")!.TextContent.Trim()).Should().Equal("Correct", "Partly correct", "Incorrect");

        questions[0].TextContent.Should().Contain("Your answer: Paris").And.Contain("Since the tenth century.");
        questions[0].QuerySelector("[data-quiz-correct-answer]").Should().BeNull("a correct answer needn't be repeated");
        questions[1].QuerySelector("[data-quiz-correct-answer]")!.TextContent.Should().Contain("Two, Three");
        questions[1].QuerySelector("[data-quiz-points]")!.TextContent.Trim().Should().Be("0.5 / 1");
        questions[2].TextContent.Should().Contain("Your answer: No answer").And.Contain("Correct answer: 4");
    }

    [Fact]
    public async Task A_score_only_quiz_shows_no_review()
    {
        _service.NextSubmitResult = new SubmitResult(true, new Dictionary<string, string[]>(),
            Quiz: new QuizResult { Score = 1, MaxScore = 1, Percent = 100 });
        await using var ctx = CreateContext();

        var cut = await SubmitAsync(ctx);

        cut.Find("[data-quiz-score]").TextContent.Should().Be("1 of 1 point");
        cut.FindAll("[data-quiz-review]").Should().BeEmpty();
    }

    [Fact]
    public async Task A_survey_that_isnt_a_quiz_shows_no_score()
    {
        _survey.IsQuiz = false;
        await using var ctx = CreateContext();

        var cut = await SubmitAsync(ctx);

        cut.FindAll("[data-quiz-result]").Should().BeEmpty();
        cut.FindAll("[data-quiz-notice]").Should().BeEmpty();
    }

    [Fact]
    public async Task Nothing_to_grade_shows_no_percentage()
    {
        await using var ctx = new BunitContext();
        ctx.Services.AddMudServices();

        var cut = ctx.Render<QuizScoreCard>(p => p.Add(x => x.Result, new QuizResult()));

        cut.Find("[data-quiz-score]").TextContent.Should().Be("0 of 0 points");
        cut.FindAll("[data-quiz-percent]").Should().BeEmpty();
    }
}
