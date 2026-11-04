using Bunit;
using FluentAssertions;
using FormFlow.Blazor.Components;
using FormFlow.Blazor.Components.Pages.Respond;
using FormFlow.Blazor.Services;
using FormFlow.Data.Models;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;

namespace FormFlow.Blazor.Tests.Respond;

public class TakeSurveyTests
{
    private readonly FakeSurveyService _service = new();
    private readonly FakeRespondentIdentity _respondent = new();
    private readonly SurveyDefinition _survey;

    private BunitContext CreateContext()
    {
        var ctx = new BunitContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        ctx.Services.AddMudServices();
        ctx.Services.AddSingleton<ISurveyService>(_service);
        ctx.Services.AddSingleton<IRespondentIdentity>(_respondent);
        ctx.Services.AddSingleton<ISurveyDrafts>(new FakeSurveyDrafts());
        ctx.Render<MudPopoverProvider>();
        return ctx;
    }

    public TakeSurveyTests()
    {

        var isStudent = new QuestionDefinition { Id = Guid.NewGuid(), Key = "is_student", Label = "Are you a student?", Type = "yes_no" };
        var campus = new QuestionDefinition
        {
            Id = Guid.NewGuid(),
            Key = "campus",
            Label = "Which campus?",
            Type = "radio",
            Options = [new Option { Label = "North", Value = "north" }, new Option { Label = "South", Value = "south" }],
            VisibleIf = new VisibleIf { Key = "is_student", ShouldEqual = true }
        };

        _survey = new SurveyDefinition
        {
            Id = Guid.NewGuid(),
            Title = "Campus survey",
            Description = "Tell us about you",
            QuestionIds = [isStudent.Id, campus.Id],
            CreatedAt = DateTime.UtcNow,
            Status = SurveyStatuses.Published,
            ShareCode = "k7m2p9qa"
        };
        _service.Surveys.Add(_survey);
        _service.Questions[_survey.Id] = [isStudent, campus];
    }

    private IRenderedComponent<TakeSurvey> RenderPage(BunitContext ctx)
    {
        var cut = ctx.Render<TakeSurvey>(p => p.Add(x => x.Id, _survey.Id));
        cut.WaitForAssertion(() => cut.Markup.Should().Contain(_survey.Title));
        return cut;
    }

    private static Task AnswerYesNoAsync(IRenderedComponent<TakeSurvey> cut, bool answer) =>
        cut.InvokeAsync(() => cut.FindComponent<MudRadioGroup<bool?>>().Instance.ValueChanged.InvokeAsync(answer));

    [Fact]
    public async Task ConditionalQuestion_IsHiddenUntilControllingAnswerMatches()
    {
        await using var ctx = CreateContext();
        var cut = RenderPage(ctx);

        cut.FindComponents<QuestionRenderer>().Should().ContainSingle();
        cut.Markup.Should().NotContain("Which campus?");
    }

    [Fact]
    public async Task AnsweringYes_ShowsConditionalQuestion()
    {
        await using var ctx = CreateContext();
        var cut = RenderPage(ctx);

        await AnswerYesNoAsync(cut, true);

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Which campus?"));
    }

    [Fact]
    public async Task Submit_SendsTrackedAnswers_AndShowsThankYou()
    {
        await using var ctx = CreateContext();
        var cut = RenderPage(ctx);
        await AnswerYesNoAsync(cut, true);
        cut.WaitForAssertion(() => cut.FindComponents<MudRadioGroup<string>>().Should().ContainSingle());
        await cut.InvokeAsync(() => cut.FindComponent<MudRadioGroup<string>>().Instance.ValueChanged.InvokeAsync("south"));

        await cut.InvokeAsync(() => cut.FindAll("button").Single(b => b.TextContent.Contains("Submit")).Click());

        _service.LastSubmitted.Should().NotBeNull();
        _service.LastSubmitted!["is_student"].Should().Equal("true");
        _service.LastSubmitted["campus"].Should().Equal("south");
        _service.LastRespondentId.Should().Be(_respondent.Id, "the server uses it to accept one answer per browser");
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Thank you!"));
    }

    private RespondentResults Summary(int total) => new()
    {
        SurveyId = _survey.Id,
        Title = _survey.Title,
        TotalResponses = total,
        Questions =
        [
            new RespondentQuestionResult
            {
                Key = "is_student", Label = "Are you a student?", Type = QuestionTypes.YesNo, AnsweredCount = total,
                Options =
                [
                    new RespondentOptionResult { Value = "true", Label = "Yes", Count = total - 1, Percent = 75 },
                    new RespondentOptionResult { Value = "false", Label = "No", Count = 1, Percent = 25 },
                ],
            },
        ],
    };

    private async Task SubmitAsync(IRenderedComponent<TakeSurvey> cut)
    {
        await AnswerYesNoAsync(cut, false);
        await cut.InvokeAsync(() => cut.FindAll("button").Single(b => b.TextContent.Contains("Submit")).Click());
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Thank you!"));
    }

    [Fact]
    public async Task AfterSubmitting_ShowsTheResultsSoFar_WhenTheSurveySharesThem()
    {
        _survey.ShowResultsToRespondents = true;
        _service.RespondentResults[_survey.Id] = Summary(total: 4);
        await using var ctx = CreateContext();
        var cut = RenderPage(ctx);

        await SubmitAsync(cut);

        cut.WaitForAssertion(() => cut.Find("[data-respondent-results]").Should().NotBeNull());
        cut.Find("[data-respondent-total]").TextContent.Should().Contain("Based on 4 responses");
        cut.Find("[data-respondent-result=is_student] [data-option-bar=Yes]").TextContent.Should().Contain("3 (75%)");
    }

    [Fact]
    public async Task AfterSubmitting_ShowsNoResults_WhenTheSurveyKeepsThemPrivate()
    {
        _service.RespondentResults[_survey.Id] = Summary(total: 4);
        await using var ctx = CreateContext();
        var cut = RenderPage(ctx);

        await SubmitAsync(cut);

        cut.FindAll("[data-respondent-results]").Should().BeEmpty();
    }

    [Fact]
    public async Task ShowLatest_ReloadsTheResults()
    {
        _survey.ShowResultsToRespondents = true;
        _service.RespondentResults[_survey.Id] = Summary(total: 4);
        await using var ctx = CreateContext();
        var cut = RenderPage(ctx);
        await SubmitAsync(cut);
        cut.WaitForAssertion(() => cut.Find("[data-respondent-total]").TextContent.Should().Contain("4 responses"));

        _service.RespondentResults[_survey.Id] = Summary(total: 9);
        await cut.InvokeAsync(() => cut.FindAll("button").Single(b => b.TextContent.Contains("Show the latest")).Click());

        cut.WaitForAssertion(() => cut.Find("[data-respondent-total]").TextContent.Should().Contain("9 responses"));
    }

    [Fact]
    public async Task ABrowserThatAlreadyAnswered_StillSeesTheSharedResults()
    {
        _survey.ShowResultsToRespondents = true;
        _service.RespondentResults[_survey.Id] = Summary(total: 2);
        _service.Answered.Add((_survey.Id, _respondent.Id));
        await using var ctx = CreateContext();
        var cut = RenderPage(ctx);

        cut.WaitForAssertion(() => cut.Find("[data-survey-closed]").TextContent.Should().Contain("already answered"));
        cut.WaitForAssertion(() => cut.Find("[data-respondent-results]").Should().NotBeNull());
    }

    [Fact]
    public async Task Submit_ShowsServerErrorsNextToQuestions()
    {
        await using var ctx = CreateContext();
        _service.NextSubmitResult = new SubmitResult(false,
            new Dictionary<string, string[]> { ["is_student"] = ["This question is required."] },
            "Please fix the highlighted answers.");
        var cut = RenderPage(ctx);

        await cut.InvokeAsync(() => cut.FindAll("button").Single(b => b.TextContent.Contains("Submit")).Click());

        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain("Please fix the highlighted answers.");
            cut.Markup.Should().Contain("This question is required.");
        });
    }

    [Fact]
    public async Task UnknownSurvey_ShowsNotAvailableMessage()
    {
        await using var ctx = CreateContext();
        var cut = ctx.Render<TakeSurvey>(p => p.Add(x => x.Id, Guid.NewGuid()));

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("does not exist"));
    }

    [Fact]
    public async Task ShareLink_OpensTheSurveyByItsCode()
    {
        await using var ctx = CreateContext();
        var cut = ctx.Render<TakeSurvey>(p => p.Add(x => x.Code, "K7M2P9QA"));

        cut.WaitForAssertion(() => cut.Markup.Should().Contain(_survey.Title));
        cut.FindComponents<QuestionRenderer>().Should().ContainSingle();
    }

    [Fact]
    public async Task UnknownShareCode_ShowsNotAvailableMessage()
    {
        await using var ctx = CreateContext();
        var cut = ctx.Render<TakeSurvey>(p => p.Add(x => x.Code, "nope2345"));

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("does not exist"));
    }

    [Fact]
    public async Task AlreadyAnswered_ShowsThankYouInsteadOfTheForm()
    {
        await using var ctx = CreateContext();
        _service.Answered.Add((_survey.Id, _respondent.Id));
        var cut = RenderPage(ctx);

        cut.WaitForAssertion(() =>
            cut.Find("[data-survey-closed]").TextContent.Should().Contain("You've already answered this survey."));
        cut.FindAll("button").Should().NotContain(b => b.TextContent.Contains("Submit"));
    }

    [Fact]
    public async Task ClosedSurvey_SaysItNoLongerTakesAnswers()
    {
        await using var ctx = CreateContext();
        _survey.ClosesAt = DateTime.UtcNow.AddMinutes(-1);
        var cut = RenderPage(ctx);

        cut.Find("[data-survey-closed]").TextContent.Should().Contain("This survey is closed");
        cut.FindComponents<QuestionRenderer>().Should().BeEmpty();
    }

    [Fact]
    public async Task Submit_RefusedForGood_ReplacesTheFormWithTheServersMessage()
    {
        await using var ctx = CreateContext();
        _service.NextSubmitResult = new SubmitResult(false, new Dictionary<string, string[]>(),
            "This survey is closed and no longer takes answers.", CanRetry: false);
        var cut = RenderPage(ctx);

        await cut.InvokeAsync(() => cut.FindAll("button").Single(b => b.TextContent.Contains("Submit")).Click());

        cut.WaitForAssertion(() =>
            cut.Find("[data-survey-closed]").TextContent.Should().Contain("This survey is closed"));
        cut.FindAll("button").Should().NotContain(b => b.TextContent.Contains("Submit"));
    }

    [Fact]
    public async Task Draft_TellsItsOwnerOnlyTheyCanOpenIt()
    {
        await using var ctx = CreateContext();
        _survey.Status = SurveyStatuses.Draft;
        var cut = RenderPage(ctx);

        cut.Find("[data-draft-notice]").TextContent.Should().Contain("This survey is a draft");
        cut.FindComponents<QuestionRenderer>().Should().ContainSingle();
    }

    [Fact]
    public async Task PublishedSurvey_ShowsNoDraftNotice()
    {
        await using var ctx = CreateContext();
        var cut = RenderPage(ctx);

        cut.FindAll("[data-draft-notice]").Should().BeEmpty();
    }

    [Fact]
    public async Task Says_so_when_the_questions_cannot_be_loaded()
    {
        _service.QuestionsUnreachable = true;
        await using var ctx = CreateContext();

        var cut = ctx.Render<TakeSurvey>(p => p.Add(x => x.Id, _survey.Id));

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Could not load this survey"));
        cut.FindAll("button").Should().NotContain(b => b.TextContent.Trim() == "Submit");
    }
}
