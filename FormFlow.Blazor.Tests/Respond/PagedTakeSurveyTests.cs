using Bunit;
using FluentAssertions;
using FormFlow.Blazor.Components;
using FormFlow.Blazor.Components.Pages.Respond;
using FormFlow.Blazor.Services;
using FormFlow.Data.Models;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Extensions;
using MudBlazor.Services;

namespace FormFlow.Blazor.Tests.Respond;

/// <summary>
/// A survey with page breaks shows one page at a time: Next checks the page, Back keeps answers,
/// Submit is on the last page, and pages whose questions are all hidden are skipped. Answers are
/// kept in the browser until they are submitted.
/// </summary>
public class PagedTakeSurveyTests
{
    private readonly FakeSurveyService _service = new();
    private readonly FakeSurveyDrafts _drafts = new();
    private readonly SurveyDefinition _survey;

    private readonly QuestionDefinition _name = new() { Id = Guid.NewGuid(), Key = "name", Label = "Your name", Type = "text" };
    private readonly QuestionDefinition _isStudent = new() { Id = Guid.NewGuid(), Key = "is_student", Label = "Are you a student?", Type = "yes_no" };
    private readonly QuestionDefinition _campus = new()
    {
        Id = Guid.NewGuid(),
        Key = "campus",
        Label = "Which campus?",
        Type = "text",
        VisibleIf = new VisibleIf { Key = "is_student", ShouldEqual = true },
    };
    private readonly QuestionDefinition _comments = new() { Id = Guid.NewGuid(), Key = "comments", Label = "Any comments?", Type = "text", Required = false };

    public PagedTakeSurveyTests()
    {
        // Page 1: name and is_student. Page 2: campus, only for students. Page 3: comments.
        _survey = new SurveyDefinition
        {
            Id = Guid.NewGuid(),
            Title = "Campus survey",
            Description = "Three pages",
            QuestionIds = [_name.Id, _isStudent.Id, _campus.Id, _comments.Id],
            PageBreaks = [_campus.Id, _comments.Id],
            CreatedAt = DateTime.UtcNow,
            Status = SurveyStatuses.Published,
        };
        _service.Surveys.Add(_survey);
        _service.Questions[_survey.Id] = [_name, _isStudent, _campus, _comments];
    }

    private BunitContext CreateContext()
    {
        var ctx = new BunitContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        ctx.Services.AddMudServices();
        ctx.Services.AddSingleton<ISurveyService>(_service);
        ctx.Services.AddSingleton<IRespondentIdentity>(new FakeRespondentIdentity());
        ctx.Services.AddSingleton<ISurveyDrafts>(_drafts);
        ctx.Render<MudPopoverProvider>();
        return ctx;
    }

    private IRenderedComponent<TakeSurvey> RenderPage(BunitContext ctx)
    {
        var cut = ctx.Render<TakeSurvey>(p => p.Add(x => x.Id, _survey.Id));
        cut.WaitForAssertion(() => cut.Markup.Should().Contain(_survey.Title));
        return cut;
    }

    private static string PageLabel(IRenderedComponent<TakeSurvey> cut) => cut.Find("[data-page-label]").TextContent.Trim();

    private static List<string> Shown(IRenderedComponent<TakeSurvey> cut) =>
        cut.FindAll("[data-question-key]").Select(e => e.GetAttribute("data-question-key")!).ToList();

    private static List<string> Buttons(IRenderedComponent<TakeSurvey> cut) =>
        cut.FindAll("button").Select(b => b.TextContent.Trim()).Where(t => t.Length > 0).ToList();

    private static Task ClickAsync(IRenderedComponent<TakeSurvey> cut, string text) =>
        cut.InvokeAsync(() => cut.FindAll("button").Single(b => b.TextContent.Trim() == text).Click());

    private static Task TypeAsync(IRenderedComponent<TakeSurvey> cut, string key, string value) =>
        cut.InvokeAsync(() => cut.FindComponents<QuestionRenderer>().Single(r => r.Instance.Question!.Key == key)
            .FindComponent<MudTextField<string>>().Instance.ValueChanged.InvokeAsync(value));

    private static Task AnswerStudentAsync(IRenderedComponent<TakeSurvey> cut, bool answer) =>
        cut.InvokeAsync(() => cut.FindComponent<MudRadioGroup<bool?>>().Instance.ValueChanged.InvokeAsync(answer));

    [Fact]
    public async Task Shows_the_first_page_with_its_progress_and_a_Next_button()
    {
        await using var ctx = CreateContext();
        var cut = RenderPage(ctx);

        // The campus page counts only once someone says they're a student.
        PageLabel(cut).Should().Be("Page 1 of 2");
        cut.FindComponent<MudProgressLinear>().Instance.GetState(x => x.Value).Should().Be(50);
        Shown(cut).Should().Equal("name", "is_student");
        Buttons(cut).Should().Contain("Next").And.NotContain("Submit").And.NotContain("Back");
    }

    [Fact]
    public async Task Next_is_blocked_by_a_missing_required_answer()
    {
        await using var ctx = CreateContext();
        var cut = RenderPage(ctx);
        await TypeAsync(cut, "name", "Ada");

        await ClickAsync(cut, "Next");

        cut.WaitForAssertion(() => cut.Find("[data-question-key=is_student]").TextContent.Should().Contain("This question is required."));
        cut.Find("[data-question-key=name]").TextContent.Should().NotContain("required");
        PageLabel(cut).Should().Be("Page 1 of 2");
        _service.LastSubmitted.Should().BeNull();
    }

    [Fact]
    public async Task Back_keeps_the_answers_and_a_conditional_page_shows_for_students()
    {
        await using var ctx = CreateContext();
        var cut = RenderPage(ctx);
        await TypeAsync(cut, "name", "Ada");
        await AnswerStudentAsync(cut, true);
        cut.WaitForAssertion(() => PageLabel(cut).Should().Be("Page 1 of 3"));

        await ClickAsync(cut, "Next");

        cut.WaitForAssertion(() => PageLabel(cut).Should().Be("Page 2 of 3"));
        Shown(cut).Should().Equal("campus");
        Buttons(cut).Should().Contain(["Back", "Next"]);

        await ClickAsync(cut, "Back");

        cut.WaitForAssertion(() => PageLabel(cut).Should().Be("Page 1 of 3"));
        cut.FindComponents<QuestionRenderer>().Single(r => r.Instance.Question!.Key == "name")
            .FindComponent<MudTextField<string>>().Instance.GetState(x => x.Value).Should().Be("Ada");
        cut.FindComponent<MudRadioGroup<bool?>>().Instance.Value.Should().BeTrue();
    }

    [Fact]
    public async Task A_page_whose_questions_are_all_hidden_is_skipped_and_Submit_is_on_the_last_page()
    {
        await using var ctx = CreateContext();
        var cut = RenderPage(ctx);
        await TypeAsync(cut, "name", "Ada");
        await AnswerStudentAsync(cut, false);

        cut.WaitForAssertion(() => PageLabel(cut).Should().Be("Page 1 of 2"));
        await ClickAsync(cut, "Next");

        cut.WaitForAssertion(() => PageLabel(cut).Should().Be("Page 2 of 2"));
        Shown(cut).Should().Equal("comments");
        cut.FindComponent<MudProgressLinear>().Instance.GetState(x => x.Value).Should().Be(100);
        Buttons(cut).Should().Contain(["Back", "Submit"]).And.NotContain("Next");

        await ClickAsync(cut, "Submit");

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Thank you!"));
        _service.LastSubmitted!["name"].Should().Equal("Ada");
        _service.LastSubmitted["is_student"].Should().Equal("false");
    }

    [Fact]
    public async Task A_server_error_goes_to_the_first_page_with_an_error()
    {
        await using var ctx = CreateContext();
        _service.NextSubmitResult = new SubmitResult(false,
            new Dictionary<string, string[]> { ["name"] = ["Minimum length is 3."] }, "Please fix the highlighted answers.");
        var cut = RenderPage(ctx);
        await TypeAsync(cut, "name", "Al");
        await AnswerStudentAsync(cut, false);
        await ClickAsync(cut, "Next");
        cut.WaitForAssertion(() => PageLabel(cut).Should().Be("Page 2 of 2"));

        await ClickAsync(cut, "Submit");

        cut.WaitForAssertion(() => PageLabel(cut).Should().Be("Page 1 of 2"));
        cut.Find("[data-question-key=name]").TextContent.Should().Contain("Minimum length is 3.");
        cut.Markup.Should().Contain("Please fix the highlighted answers.");
    }

    [Fact]
    public async Task A_survey_without_page_breaks_is_one_page_with_Submit()
    {
        _survey.PageBreaks = [];
        await using var ctx = CreateContext();
        var cut = RenderPage(ctx);

        cut.FindAll("[data-survey-progress]").Should().BeEmpty();
        Shown(cut).Should().Equal("name", "is_student", "comments");
        Buttons(cut).Should().Contain("Submit").And.NotContain("Next");
    }

    [Fact]
    public async Task Answers_are_saved_as_they_change_and_cleared_after_submitting()
    {
        _survey.PageBreaks = [];
        await using var ctx = CreateContext();
        var cut = RenderPage(ctx);

        await TypeAsync(cut, "name", "Ada");
        await AnswerStudentAsync(cut, false);

        _drafts.Saved[_survey.Id]["name"].Should().Equal("Ada");
        _drafts.Saved[_survey.Id]["is_student"].Should().Equal("false");

        await ClickAsync(cut, "Submit");

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Thank you!"));
        _drafts.Saved.Should().NotContainKey(_survey.Id);
    }

    [Fact]
    public async Task Saved_answers_come_back_with_a_note_and_Start_over_clears_them()
    {
        _drafts.Saved[_survey.Id] = new() { ["name"] = ["Ada"], ["is_student"] = ["true"], ["gone"] = ["x"] };
        await using var ctx = CreateContext();
        var cut = RenderPage(ctx);

        cut.WaitForAssertion(() => cut.Find("[data-resume-note]").TextContent.Should().Contain("We saved your answers on this device."));
        cut.FindComponents<QuestionRenderer>().Single(r => r.Instance.Question!.Key == "name")
            .FindComponent<MudTextField<string>>().Instance.GetState(x => x.Value).Should().Be("Ada");
        cut.FindComponent<MudRadioGroup<bool?>>().Instance.Value.Should().BeTrue();

        await ClickAsync(cut, "Start over");

        cut.WaitForAssertion(() => cut.FindAll("[data-resume-note]").Should().BeEmpty());
        _drafts.Saved.Should().NotContainKey(_survey.Id);
        cut.FindComponents<QuestionRenderer>().Single(r => r.Instance.Question!.Key == "name")
            .FindComponent<MudTextField<string>>().Instance.GetState(x => x.Value).Should().BeNullOrEmpty();
    }

    [Fact]
    public async Task Saved_answers_are_not_restored_once_the_browser_has_answered()
    {
        var respondent = new FakeRespondentIdentity();
        _service.Answered.Add((_survey.Id, respondent.Id));
        _drafts.Saved[_survey.Id] = new() { ["name"] = ["Ada"] };
        await using var ctx = CreateContext();
        var cut = RenderPage(ctx);

        cut.WaitForAssertion(() => cut.Find("[data-survey-closed]").TextContent.Should().Contain("You've already answered"));
        cut.FindAll("[data-resume-note]").Should().BeEmpty();
    }
}
