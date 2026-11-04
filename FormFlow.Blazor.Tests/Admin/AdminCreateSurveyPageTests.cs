using Bunit;
using FluentAssertions;
using FormFlow.Blazor.Components.Pages.Admin;
using FormFlow.Blazor.Services;
using FormFlow.Blazor.Tests.Respond;
using FormFlow.Data.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Extensions;
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

    private static bool SaveDisabled(IRenderedComponent<AdminCreateSurvey> cut) =>
        cut.FindAll("button").Single(b => b.TextContent.Trim() == "Save Survey").HasAttribute("disabled");

    /// <summary>The numbered rows of the "Selected Questions" list.</summary>
    private static List<string> Selected(IRenderedComponent<AdminCreateSurvey> cut) =>
        cut.FindAll("p").Select(p => p.TextContent.Trim()).Where(t => t.Length > 2 && char.IsDigit(t[0]) && t.Contains(". ")).ToList();

    [Fact]
    public async Task Save_is_disabled_until_the_required_fields_are_filled()
    {
        await using var ctx = CreateContext();
        var cut = ctx.Render<AdminCreateSurvey>();
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Which campus?"));

        ClickButton(cut, "Add", 0);
        cut.WaitForAssertion(() => Selected(cut).Should().Equal("1. Are you a student?"));
        SaveDisabled(cut).Should().BeTrue("the title and description are still empty");

        Type(cut, "input", "Campus life");
        Type(cut, "textarea", "About campus");

        cut.WaitForAssertion(() => SaveDisabled(cut).Should().BeFalse());
    }

    [Fact]
    public async Task A_question_can_only_be_added_once()
    {
        await using var ctx = CreateContext();
        var cut = ctx.Render<AdminCreateSurvey>();
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Which campus?"));

        ClickButton(cut, "Add", 0);

        cut.WaitForAssertion(() => Selected(cut).Should().Equal("1. Are you a student?"));
        var added = cut.FindAll("button").Single(b => b.TextContent.Trim() == "Added");
        added.HasAttribute("disabled").Should().BeTrue();
        cut.FindAll("button").Where(b => b.TextContent.Trim() == "Add").Should().ContainSingle("only the other question can still be added");
    }

    [Fact]
    public async Task Removing_a_question_keeps_the_others_and_lets_it_be_added_again()
    {
        await using var ctx = CreateContext();
        var cut = ctx.Render<AdminCreateSurvey>();
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Which campus?"));
        ClickButton(cut, "Add", 0);
        ClickButton(cut, "Add", 0);
        cut.WaitForAssertion(() => Selected(cut).Should().Equal("1. Are you a student?", "2. Which campus?"));

        ClickButton(cut, "Remove", 0);

        cut.WaitForAssertion(() => Selected(cut).Should().Equal("1. Which campus?"));
        cut.FindAll("button").Where(b => b.TextContent.Trim() == "Add").Should().ContainSingle();
    }

    [Fact]
    public async Task Removing_the_last_question_disables_save()
    {
        await using var ctx = CreateContext();
        var cut = ctx.Render<AdminCreateSurvey>();
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Which campus?"));
        Type(cut, "input", "Campus life");
        Type(cut, "textarea", "About campus");
        ClickButton(cut, "Add", 0);
        cut.WaitForAssertion(() => SaveDisabled(cut).Should().BeFalse());

        ClickButton(cut, "Remove");

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("No questions selected yet."));
        SaveDisabled(cut).Should().BeTrue();
    }

    [Fact]
    public async Task Says_so_when_the_questions_cannot_be_loaded()
    {
        _questions.Unreachable = true;
        await using var ctx = CreateContext();

        var cut = ctx.Render<AdminCreateSurvey>();

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Could not load questions."));
        cut.Markup.Should().NotContain("No questions found.");
        cut.FindAll(".mud-progress-circular").Should().BeEmpty();
    }

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
        var nav = ctx.Services.GetRequiredService<NavigationManager>();
        cut.WaitForAssertion(() => nav.Uri.Should().EndWith("/admin/surveys"));
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

    private void AddTemplates()
    {
        _surveys.Templates.AddRange([
            new SurveyTemplate { Id = Guid.NewGuid(), Title = "Course evaluation", Description = "End-of-term feedback", QuestionCount = 9, PageCount = 3 },
            new SurveyTemplate { Id = Guid.NewGuid(), Title = "Event feedback", Description = "After an event", QuestionCount = 5 },
        ]);
    }

    [Fact]
    public async Task A_new_survey_starts_with_the_template_gallery()
    {
        AddTemplates();
        await using var ctx = CreateContext();
        var cut = ctx.Render<AdminCreateSurvey>();

        cut.WaitForAssertion(() => cut.FindAll("[data-template-gallery]").Should().ContainSingle());
        var cards = cut.FindAll("[data-template]");
        cards.Select(c => c.GetAttribute("data-template")).Should().Equal("blank", "Course evaluation", "Event feedback");
        cards[1].TextContent.Should().Contain("End-of-term feedback").And.Contain("9 questions on 3 pages");
        cards[2].TextContent.Should().Contain("5 questions").And.NotContain("pages");
        cut.FindAll("button[aria-label='Use the Course evaluation template']").Should().ContainSingle();
        cut.FindAll("input").Should().BeEmpty("the form shows once a starting point is picked");
    }

    [Fact]
    public async Task Start_blank_shows_the_empty_form()
    {
        AddTemplates();
        await using var ctx = CreateContext();
        var cut = ctx.Render<AdminCreateSurvey>();
        cut.WaitForAssertion(() => cut.FindAll("[data-template-gallery]").Should().ContainSingle());

        ClickButton(cut, "Start blank");

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Which campus?"));
        cut.FindAll("[data-template-gallery]").Should().BeEmpty();
        cut.Markup.Should().Contain("No questions selected yet.");
    }

    [Fact]
    public async Task Using_a_template_opens_the_new_draft_for_editing()
    {
        AddTemplates();
        await using var ctx = CreateContext();
        var snackbars = ctx.Render<MudSnackbarProvider>();
        var cut = ctx.Render<AdminCreateSurvey>();
        cut.WaitForAssertion(() => cut.FindAll("[data-template-gallery]").Should().ContainSingle());

        cut.Find("button[aria-label='Use the Course evaluation template']").Click();

        cut.WaitForAssertion(() => _surveys.UsedTemplates.Should().Equal(_surveys.Templates[0].Id));
        var draft = _surveys.Surveys.Single();
        var nav = ctx.Services.GetRequiredService<NavigationManager>();
        nav.Uri.Should().EndWith($"/admin/surveys/{draft.Id}/edit");
        snackbars.WaitForAssertion(() => snackbars.Markup.Should().Contain("Created \"Course evaluation\" from the template as a draft."));
    }

    [Fact]
    public async Task Without_templates_a_new_survey_goes_straight_to_the_form()
    {
        _surveys.TemplatesUnreachable = true;
        await using var ctx = CreateContext();
        var cut = ctx.Render<AdminCreateSurvey>();

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Which campus?"));
        cut.FindAll("[data-template-gallery]").Should().BeEmpty();
    }

    [Fact]
    public async Task Editing_never_shows_the_template_gallery()
    {
        AddTemplates();
        var survey = new SurveyDefinition
        {
            Id = Guid.NewGuid(),
            Title = "Campus life",
            Description = "About campus",
            QuestionIds = [_isStudent.Id],
            CreatedAt = DateTime.UtcNow,
        };
        _surveys.Surveys.Add(survey);
        await using var ctx = CreateContext();

        var cut = ctx.Render<AdminCreateSurvey>(p => p.Add(x => x.Id, survey.Id));

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("1. Are you a student?"));
        cut.FindAll("[data-template-gallery]").Should().BeEmpty();
    }

    private static IRenderedComponent<MudSwitch<bool>> PageSwitch(IRenderedComponent<AdminCreateSurvey> cut, string key) =>
        PageSwitches(cut).Single(s => s.Find("[data-page-break]").GetAttribute("data-page-break") == key);

    /// <summary>The "New page starts here" switches, leaving out the quiz switch.</summary>
    private static List<IRenderedComponent<MudSwitch<bool>>> PageSwitches(IRenderedComponent<AdminCreateSurvey> cut) =>
        cut.FindComponents<MudSwitch<bool>>().Where(s => s.Instance.Label == "New page starts here").ToList();

    [Fact]
    public async Task Page_toggles_split_the_selected_questions_into_pages_and_are_saved()
    {
        var third = new QuestionDefinition { Id = Guid.NewGuid(), Key = "comments", Label = "Any comments?", Type = "text" };
        _questions.Questions.Add(third);
        await using var ctx = CreateContext();
        var cut = ctx.Render<AdminCreateSurvey>();
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Any comments?"));
        Type(cut, "input", "Campus life");
        Type(cut, "textarea", "About campus");
        ClickButton(cut, "Add", 0);
        ClickButton(cut, "Add", 0);
        ClickButton(cut, "Add", 0);

        // Every question but the first can start a page; none do yet, so there are no separators.
        cut.WaitForAssertion(() => PageSwitches(cut).Should().HaveCount(2));
        cut.FindAll("[data-page-separator]").Should().BeEmpty();

        await cut.InvokeAsync(() => PageSwitch(cut, "comments").Instance.ValueChanged.InvokeAsync(true));

        cut.WaitForAssertion(() => cut.FindAll("[data-page-separator]").Select(s => s.TextContent.Trim()).Should().Equal("Page 1", "Page 2"));
        cut.FindAll("[data-page-separator]")[1].NextElementSibling!.TextContent.Should().Contain("3. Any comments?");

        await cut.InvokeAsync(() => PageSwitch(cut, "campus").Instance.ValueChanged.InvokeAsync(true));
        cut.WaitForAssertion(() => cut.FindAll("[data-page-separator]").Should().HaveCount(3));

        cut.WaitForAssertion(() => SaveDisabled(cut).Should().BeFalse());
        ClickButton(cut, "Save Survey");

        cut.WaitForAssertion(() => _surveys.LastCreated.Should().NotBeNull());
        _surveys.LastCreated!.PageBreaks.Should().Equal(_campus.Id, third.Id);
    }

    [Fact]
    public async Task Edit_mode_shows_the_saved_pages_and_moving_a_question_first_drops_its_page_break()
    {
        var survey = new SurveyDefinition
        {
            Id = Guid.NewGuid(),
            Title = "Campus life",
            Description = "About campus",
            QuestionIds = [_isStudent.Id, _campus.Id],
            PageBreaks = [_campus.Id],
            CreatedAt = DateTime.UtcNow,
        };
        _surveys.Surveys.Add(survey);
        await using var ctx = CreateContext();
        var cut = ctx.Render<AdminCreateSurvey>(p => p.Add(x => x.Id, survey.Id));
        cut.WaitForAssertion(() => cut.FindAll("[data-page-separator]").Should().HaveCount(2));
        PageSwitch(cut, "campus").Instance.GetState(x => x.Value).Should().BeTrue();

        cut.Find("button[aria-label='Move down']").Click();

        cut.WaitForAssertion(() => cut.FindAll("[data-page-separator]").Should().BeEmpty());
        ClickButton(cut, "Save Changes");
        cut.WaitForAssertion(() => _surveys.LastUpdated.Should().NotBeNull());
        _surveys.LastUpdated!.Value.Survey.QuestionIds.Should().Equal(_campus.Id, _isStudent.Id);
        _surveys.LastUpdated.Value.Survey.PageBreaks.Should().BeEmpty();
    }

    [Fact]
    public async Task Edit_mode_shows_not_found_for_a_missing_survey()
    {
        await using var ctx = CreateContext();
        var cut = ctx.Render<AdminCreateSurvey>(p => p.Add(x => x.Id, Guid.NewGuid()));

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Survey not found."));
    }
    [Fact]
    public async Task Edit_mode_refuses_a_professor_who_did_not_create_the_survey()
    {
        await using var ctx = CreateContext();
        var survey = new SurveyDefinition
        {
            Id = Guid.NewGuid(),
            Title = "Demo",
            Description = "Seeded",
            QuestionIds = [_isStudent.Id],
            CreatedAt = DateTime.UtcNow
        };
        _surveys.Surveys.Add(survey);

        var cut = ctx.Render<AdminCreateSurvey>(p => p
            .Add(x => x.Id, survey.Id)
            .AddCascadingValue(new AdminAccess("professor", Guid.NewGuid())));

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("You can only edit surveys you created."));
        cut.FindAll("button").Should().NotContain(b => b.TextContent.Trim() == "Save Survey");
    }

    private readonly QuestionDefinition _capital = new()
    {
        Id = Guid.NewGuid(),
        Key = "capital",
        Label = "Capital of France?",
        Type = "radio",
        Options = [new() { Value = "paris", Label = "Paris" }, new() { Value = "lyon", Label = "Lyon" }],
        AnswerKey = new AnswerKey { CorrectAnswers = ["paris"], Points = 2 },
    };

    /// <summary>The line saying how many selected questions are graded, with its whitespace collapsed.</summary>
    private static string Graded(IRenderedComponent<AdminCreateSurvey> cut) =>
        string.Join(" ", cut.Find("[data-quiz-graded]").TextContent.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static Task TurnOnQuizAsync(IRenderedComponent<AdminCreateSurvey> cut) =>
        cut.InvokeAsync(() => cut.FindComponents<MudSwitch<bool>>()
            .Single(s => s.Instance.Label == "Grade answers as a quiz").Instance.ValueChanged.InvokeAsync(true));

    [Fact]
    public async Task Quiz_settings_are_hidden_until_the_survey_is_a_quiz_and_are_saved()
    {
        _questions.Questions.Add(_capital);
        await using var ctx = CreateContext();
        var cut = ctx.Render<AdminCreateSurvey>();
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Capital of France?"));
        cut.FindAll("[data-quiz-settings]").Should().BeEmpty();

        Type(cut, "input", "Geography quiz");
        Type(cut, "textarea", "No notes");
        ClickButton(cut, "Add", 2);
        await TurnOnQuizAsync(cut);
        cut.WaitForAssertion(() => cut.FindAll("[data-quiz-settings]").Should().ContainSingle());
        Graded(cut).Should().Contain("1 of 1 question is graded, worth 2 points in all.");
        cut.Find("[data-graded-chip]").TextContent.Trim().Should().Be("2 points");

        var feedback = cut.FindComponent<MudRadioGroup<string>>();
        feedback.Instance.Value.Should().Be(QuizFeedbackModes.Score, "students only see their score unless the builder says otherwise");
        await cut.InvokeAsync(() => feedback.Instance.ValueChanged.InvokeAsync(QuizFeedbackModes.Answers));
        ClickButton(cut, "Save Survey");

        cut.WaitForAssertion(() => _surveys.LastCreated.Should().NotBeNull());
        _surveys.LastCreated!.IsQuiz.Should().BeTrue();
        _surveys.LastCreated.QuizFeedback.Should().Be(QuizFeedbackModes.Answers);
    }

    [Fact]
    public async Task Quiz_settings_warn_when_no_selected_question_has_an_answer_key()
    {
        await using var ctx = CreateContext();
        var cut = ctx.Render<AdminCreateSurvey>();
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Which campus?"));

        ClickButton(cut, "Add", 0);
        await TurnOnQuizAsync(cut);

        cut.WaitForAssertion(() => cut.Find("[data-quiz-graded]").TextContent.Should().Contain("nothing will be graded"));
        cut.FindAll("[data-graded-chip]").Should().BeEmpty();
    }

    [Fact]
    public async Task Edit_mode_loads_the_quiz_settings_and_the_answer_keys_of_the_surveys_questions()
    {
        // The bank hides answer keys of questions this account doesn't manage; the survey's questions carry them.
        var hidden = new QuestionDefinition
        {
            Id = _capital.Id,
            Key = _capital.Key,
            Label = _capital.Label,
            Type = _capital.Type,
            Options = _capital.Options,
        };
        _questions.Questions.Add(hidden);
        var survey = new SurveyDefinition
        {
            Id = Guid.NewGuid(),
            Title = "Geography quiz",
            Description = "No notes",
            QuestionIds = [_isStudent.Id, _capital.Id],
            CreatedAt = DateTime.UtcNow,
            IsQuiz = true,
            QuizFeedback = QuizFeedbackModes.Answers,
        };
        _surveys.Surveys.Add(survey);
        _surveys.Questions[survey.Id] = [_isStudent, _capital];
        await using var ctx = CreateContext();

        var cut = ctx.Render<AdminCreateSurvey>(p => p.Add(x => x.Id, survey.Id));

        cut.WaitForAssertion(() => cut.FindAll("[data-quiz-settings]").Should().ContainSingle());
        cut.FindComponent<MudRadioGroup<string>>().Instance.Value.Should().Be(QuizFeedbackModes.Answers);
        Graded(cut).Should().Contain("1 of 2 questions are graded, worth 2 points in all.");

        ClickButton(cut, "Save Changes");
        cut.WaitForAssertion(() => _surveys.LastUpdated.Should().NotBeNull());
        _surveys.LastUpdated!.Value.Survey.IsQuiz.Should().BeTrue();
        _surveys.LastUpdated.Value.Survey.QuizFeedback.Should().Be(QuizFeedbackModes.Answers);
    }

    [Fact]
    public async Task The_template_gallery_marks_quizzes()
    {
        AddTemplates();
        _surveys.Templates.Add(new SurveyTemplate { Id = Guid.NewGuid(), Title = "Quiz", Description = "Graded", QuestionCount = 6, IsQuiz = true });
        await using var ctx = CreateContext();
        var cut = ctx.Render<AdminCreateSurvey>();

        cut.WaitForAssertion(() => cut.FindAll("[data-template]").Should().HaveCount(4));
        cut.FindAll("[data-template-quiz]").Should().ContainSingle();
        cut.Find("[data-template='Quiz'] [data-template-quiz]").TextContent.Trim().Should().Be("Quiz");
    }
}
