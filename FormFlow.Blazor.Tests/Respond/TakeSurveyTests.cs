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
    private readonly SurveyDefinition _survey;

    private BunitContext CreateContext()
    {
        var ctx = new BunitContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        ctx.Services.AddMudServices();
        ctx.Services.AddSingleton<ISurveyService>(_service);
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
            CreatedAt = DateTime.UtcNow
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
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Thank you!"));
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
}
