using Bunit;
using FluentAssertions;
using FormFlow.Blazor.Components.Pages.Admin;
using FormFlow.Blazor.Services;
using FormFlow.Blazor.Tests.Respond;
using FormFlow.Data.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;

namespace FormFlow.Blazor.Tests.Admin;

public class AdminCreateSurveyPageTests
{
    private readonly InMemoryQuestionService _questions = new();
    private readonly FakeSurveyService _surveys = new();
    private readonly QuestionDefinition _isStudent = new() { Id = Guid.NewGuid(), Key = "is_student", Label = "Are you a student?", Type = "yes_no" };
    private readonly QuestionDefinition _campus = new()
    {
        Id = Guid.NewGuid(),
        Key = "campus",
        Label = "Which campus?",
        Type = "text",
        VisibleIf = new VisibleIf { Key = "is_student", ShouldEqual = true }
    };

    public AdminCreateSurveyPageTests()
    {
        _questions.Questions.AddRange([_isStudent, _campus]);
    }

    private BunitContext CreateContext()
    {
        var ctx = new BunitContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        ctx.Services.AddMudServices();
        ctx.Services.AddSingleton<IQuestionService>(_questions);
        ctx.Services.AddSingleton<ISurveyService>(_surveys);
        ctx.Render<MudPopoverProvider>();
        return ctx;
    }

    private static void ClickButton(IRenderedComponent<AdminCreateSurvey> cut, string text, int index = 0) =>
        cut.FindAll("button").Where(b => b.TextContent.Trim() == text).ElementAt(index).Click();

    private static void Type(IRenderedComponent<AdminCreateSurvey> cut, string selector, string value) =>
        cut.Find(selector).Input(value);

    [Fact]
    public async Task Warns_about_conditional_questions_until_they_follow_their_controller()
    {
        await using var ctx = CreateContext();
        var cut = ctx.Render<AdminCreateSurvey>();
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Which campus?"));

        ClickButton(cut, "Add", 1); // Which campus?
        cut.Markup.Should().Contain("which is not in this survey, so it will never show");

        ClickButton(cut, "Add", 0); // Are you a student?
        cut.Markup.Should().Contain("appears before the question it depends on");

        cut.Find("button[aria-label='Move down']").Click();
        cut.Markup.Should().NotContain("appears before the question it depends on");
        cut.Markup.Should().NotContain("will never show");
    }

    [Fact]
    public async Task Saves_a_new_survey_with_questions_in_the_chosen_order()
    {
        await using var ctx = CreateContext();
        var cut = ctx.Render<AdminCreateSurvey>();
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Which campus?"));

        Type(cut, "input", "Campus life");
        Type(cut, "textarea", "About campus");
        ClickButton(cut, "Add", 1);
        ClickButton(cut, "Add", 0);
        cut.Find("button[aria-label='Move down']").Click();

        cut.WaitForAssertion(() => cut.FindAll("button").Single(b => b.TextContent.Trim() == "Save Survey").HasAttribute("disabled").Should().BeFalse());
        ClickButton(cut, "Save Survey");

        cut.WaitForAssertion(() => _surveys.LastCreated.Should().NotBeNull());
        _surveys.LastCreated!.Title.Should().Be("Campus life");
        _surveys.LastCreated.QuestionIds.Should().Equal(_isStudent.Id, _campus.Id);
        ctx.Services.GetRequiredService<NavigationManager>().Uri.Should().EndWith("/admin/surveys");
    }

    [Fact]
    public async Task Save_is_disabled_without_questions()
    {
        await using var ctx = CreateContext();
        var cut = ctx.Render<AdminCreateSurvey>();
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Which campus?"));

        Type(cut, "input", "Campus life");
        Type(cut, "textarea", "About campus");

        cut.FindAll("button").Single(b => b.TextContent.Trim() == "Save Survey").HasAttribute("disabled").Should().BeTrue();
    }

    [Fact]
    public async Task Edit_mode_loads_the_survey_and_saves_changes()
    {
        var survey = new SurveyDefinition
        {
            Id = Guid.NewGuid(),
            Title = "Campus life",
            Description = "About campus",
            QuestionIds = [_isStudent.Id, _campus.Id],
            CreatedAt = DateTime.UtcNow
        };
        _surveys.Surveys.Add(survey);
        await using var ctx = CreateContext();
        var cut = ctx.Render<AdminCreateSurvey>(p => p.Add(x => x.Id, survey.Id));
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("1. Are you a student?"));
        cut.Markup.Should().Contain("2. Which campus?");

        ClickButton(cut, "Remove", 1);
        ClickButton(cut, "Save Changes");

        cut.WaitForAssertion(() => _surveys.LastUpdated.Should().NotBeNull());
        _surveys.LastUpdated!.Value.Id.Should().Be(survey.Id);
        _surveys.LastUpdated.Value.Survey.QuestionIds.Should().Equal(_isStudent.Id);
    }

    [Fact]
    public async Task Shows_the_api_error_when_saving_fails()
    {
        _surveys.NextSaveResult = (false, "Unknown question ids: 123");
        await using var ctx = CreateContext();
        var snackbars = ctx.Render<MudSnackbarProvider>();
        var cut = ctx.Render<AdminCreateSurvey>();
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Which campus?"));

        Type(cut, "input", "Campus life");
        Type(cut, "textarea", "About campus");
        ClickButton(cut, "Add", 0);
        cut.WaitForAssertion(() => cut.FindAll("button").Single(b => b.TextContent.Trim() == "Save Survey").HasAttribute("disabled").Should().BeFalse());
        ClickButton(cut, "Save Survey");

        snackbars.WaitForAssertion(() => snackbars.Markup.Should().Contain("Unknown question ids: 123"));
        ctx.Services.GetRequiredService<NavigationManager>().Uri.Should().NotEndWith("/admin/surveys");
    }

    [Fact]
    public async Task Edit_mode_shows_not_found_for_a_missing_survey()
    {
        await using var ctx = CreateContext();
        var cut = ctx.Render<AdminCreateSurvey>(p => p.Add(x => x.Id, Guid.NewGuid()));

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Survey not found."));
    }
}
