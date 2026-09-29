using Bunit;
using FluentAssertions;
using FormFlow.Blazor.Components.Pages.Admin;
using FormFlow.Blazor.Services;
using FormFlow.Data.Models;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;

namespace FormFlow.Blazor.Tests.Admin;

public class AdminQuestionsPageTests
{
    private readonly InMemoryQuestionService _questions = new();

    public AdminQuestionsPageTests()
    {
        _questions.Questions.Add(new QuestionDefinition { Id = Guid.NewGuid(), Key = "is_student", Label = "Are you a student?", Type = "yes_no" });
        _questions.Questions.Add(new QuestionDefinition
        {
            Id = Guid.NewGuid(),
            Key = "campus",
            Label = "Which campus?",
            Type = "text",
            VisibleIf = new VisibleIf { Key = "is_student", ShouldEqual = true }
        });
    }

    private BunitContext CreateContext()
    {
        var ctx = new BunitContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        ctx.Services.AddMudServices();
        ctx.Services.AddSingleton<IQuestionService>(_questions);
        ctx.Render<MudPopoverProvider>();
        return ctx;
    }

    [Fact]
    public async Task Professor_can_edit_only_their_own_questions_but_sees_the_whole_bank()
    {
        var me = Guid.NewGuid();
        _questions.Questions[0].OwnerId = me;
        _questions.Questions[0].OwnerName = "professor";
        await using var ctx = CreateContext();
        var cut = ctx.Render<AdminQuestions>(p => p.AddCascadingValue(new AdminAccess("professor", me)));

        cut.WaitForAssertion(() => cut.FindAll("tbody tr").Should().HaveCount(2));
        cut.Markup.Should().Contain("Create Question");
        var rows = cut.FindAll("tbody tr");
        rows[0].TextContent.Should().Contain("professor");
        rows[0].QuerySelectorAll("button").Should().Contain(b => b.TextContent.Trim() == "Edit");
        rows[1].TextContent.Should().Contain("Administrators", "the seeded questions belong to the administrators");
        rows[1].QuerySelectorAll("button").Should().BeEmpty();
    }

    [Fact]
    public async Task Lists_questions_with_when_they_are_shown()
    {
        await using var ctx = CreateContext();
        var cut = ctx.Render<AdminQuestions>();

        cut.WaitForAssertion(() => cut.FindAll("tbody tr").Should().HaveCount(2));
        cut.Markup.Should().Contain("Always");
        cut.Markup.Should().Contain("is_student is yes");
    }

    [Fact]
    public async Task Shows_an_empty_state()
    {
        _questions.Questions.Clear();
        await using var ctx = CreateContext();
        var cut = ctx.Render<AdminQuestions>();

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("No questions found."));
    }

    [Fact]
    public async Task Edit_opens_the_edit_page()
    {
        await using var ctx = CreateContext();
        var cut = ctx.Render<AdminQuestions>();
        cut.WaitForAssertion(() => cut.FindAll("tbody tr").Should().HaveCount(2));

        cut.FindAll("button").First(b => b.TextContent.Trim() == "Edit").Click();

        var nav = ctx.Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>();
        cut.WaitForAssertion(() => nav.Uri.Should().EndWith($"/admin/questions/{_questions.Questions[0].Id}/edit"));
    }

    [Fact]
    public async Task Confirmed_delete_removes_the_row()
    {
        await using var ctx = CreateContext();
        var dialogs = ctx.Render<MudDialogProvider>();
        var cut = ctx.Render<AdminQuestions>();
        cut.WaitForAssertion(() => cut.FindAll("tbody tr").Should().HaveCount(2));

        cut.Find("button[aria-label='Delete Which campus?']").Click();
        dialogs.WaitForAssertion(() => dialogs.Markup.Should().Contain("This cannot be undone."));
        dialogs.FindAll("button").First(b => b.TextContent.Trim() == "Delete").Click();

        cut.WaitForAssertion(() => cut.FindAll("tbody tr").Should().HaveCount(1));
        _questions.Deleted.Should().Equal(_questions.Questions[1].Id);
    }

    [Fact]
    public async Task Refused_delete_keeps_the_row_and_shows_why()
    {
        _questions.NextDeleteResult = (false, "This question is used by: Campus survey.");
        await using var ctx = CreateContext();
        var dialogs = ctx.Render<MudDialogProvider>();
        var snackbars = ctx.Render<MudSnackbarProvider>();
        var cut = ctx.Render<AdminQuestions>();
        cut.WaitForAssertion(() => cut.FindAll("tbody tr").Should().HaveCount(2));

        cut.Find("button[aria-label='Delete Are you a student?']").Click();
        dialogs.WaitForAssertion(() => dialogs.FindAll("button").Should().Contain(b => b.TextContent.Trim() == "Delete"));
        dialogs.FindAll("button").First(b => b.TextContent.Trim() == "Delete").Click();

        snackbars.WaitForAssertion(() => snackbars.Markup.Should().Contain("This question is used by: Campus survey."));
        cut.FindAll("tbody tr").Should().HaveCount(2);
    }

    [Fact]
    public async Task Cancelled_delete_does_nothing()
    {
        await using var ctx = CreateContext();
        var dialogs = ctx.Render<MudDialogProvider>();
        var cut = ctx.Render<AdminQuestions>();
        cut.WaitForAssertion(() => cut.FindAll("tbody tr").Should().HaveCount(2));

        cut.Find("button[aria-label='Delete Which campus?']").Click();
        dialogs.WaitForAssertion(() => dialogs.FindAll("button").Should().Contain(b => b.TextContent.Trim() == "Cancel"));
        dialogs.FindAll("button").First(b => b.TextContent.Trim() == "Cancel").Click();

        _questions.Deleted.Should().BeEmpty();
        cut.FindAll("tbody tr").Should().HaveCount(2);
    }
}
