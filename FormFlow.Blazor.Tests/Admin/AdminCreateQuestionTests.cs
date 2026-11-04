using System.Reflection;
using Bunit;
using FluentAssertions;
using FormFlow.Blazor.Components.Pages.Admin;
using FormFlow.Blazor.Services;
using FormFlow.Data.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Extensions;
using MudBlazor.Services;

namespace FormFlow.Blazor.Tests.Admin;

file sealed class FakeQuestionService : IQuestionService
{
    public (bool Success, string? Error) NextResult { get; set; } = (true, null);
    public NewQuestion? LastPayload { get; private set; }
    public int CreateCallCount { get; private set; }

    public Task<List<QuestionDefinition>?> GetAllQuestionsAsync()
        => Task.FromResult<List<QuestionDefinition>?>(new());

    public QuestionDefinition? Existing { get; set; }
    public NewQuestion? LastUpdate { get; private set; }
    public int UpdateCallCount { get; private set; }

    /// <summary>When set, saves wait until the test completes it, as a slow server would.</summary>
    public TaskCompletionSource<(bool Success, string? Error)>? PendingSave { get; set; }

    public Task<QuestionDefinition?> GetQuestionAsync(Guid id)
        => Task.FromResult(Existing?.Id == id ? Existing : null);

    public Task<(bool Success, string? Error)> UpdateQuestionAsync(Guid id, NewQuestion question)
    {
        LastUpdate = question;
        UpdateCallCount++;
        return PendingSave?.Task ?? Task.FromResult(NextResult);
    }

    public Task<(bool Success, string? Error)> DeleteQuestionAsync(Guid id)
        => Task.FromResult(NextResult);

    public Task<(bool Success, string? Error)> CreateQuestionAsync(NewQuestion newQuestion)
    {
        LastPayload = newQuestion;
        CreateCallCount++;
        return Task.FromResult(NextResult);
    }
}

public class AdminCreateQuestionTests
{
    [Fact]
    public async Task Form_Renders_All_NewQuestion_Fields()
    {
        await using var ctx = CreateContext();
        var cut = ctx.Render<AdminCreateQuestion>();

        cut.Markup.Should().Contain("Create Question");
        cut.Markup.Should().Contain("Label");
        cut.Markup.Should().Contain("Key");
        cut.Markup.Should().Contain("Type");
        cut.Markup.Should().Contain("Required");
        cut.Markup.Should().Contain("Placeholder");
        cut.Markup.Should().Contain("Default Value");
        cut.Markup.Should().Contain("Help Text");

        var items = cut.FindComponents<MudSelectItem<string>>();
        items.Select(i => i.Instance.Value).Should().BeEquivalentTo(QuestionTypes.All, "every question type can be picked");
    }

    [Fact]
    public async Task CreateButton_Is_Disabled_When_Form_Is_Empty()
    {
        await using var ctx = CreateContext();
        var cut = ctx.Render<AdminCreateQuestion>();

        var button = cut.FindComponents<MudButton>()
            .First(b => b.Markup.Contains("Create Question"));

        button.Instance.Disabled.Should().BeTrue();
    }

    [Theory]
    [InlineData("dropdown")]
    [InlineData("radio")]
    [InlineData("checkbox")]
    [InlineData("multiselect")]
    [InlineData("likert")]
    public async Task OptionsEditor_Appears_For_OptionBasedTypes(string type)
    {
        await using var ctx = CreateContext();
        var cut = ctx.Render<AdminCreateQuestion>();

        await SetTypeAsync(cut, type);

        cut.WaitForAssertion(() =>
            cut.Markup.Should().Contain("Add Option"));
    }

    [Theory]
    [InlineData("text")]
    [InlineData("number")]
    [InlineData("yes_no")]
    [InlineData("long_text")]
    [InlineData("email")]
    [InlineData("date")]
    [InlineData("rating")]
    [InlineData("nps")]
    [InlineData("slider")]
    public async Task OptionsEditor_Hidden_For_NonOptionTypes(string type)
    {
        await using var ctx = CreateContext();
        var cut = ctx.Render<AdminCreateQuestion>();

        await SetTypeAsync(cut, type);

        cut.WaitForAssertion(() =>
            cut.Markup.Should().NotContain("Add Option"));
    }

    [Fact]
    public async Task AddOption_AddsOptionRow()
    {
        await using var ctx = CreateContext();
        var cut = ctx.Render<AdminCreateQuestion>();

        await SetTypeAsync(cut, "dropdown");

        cut.WaitForAssertion(() =>
            cut.Markup.Should().Contain("Add Option"));

        await InvokeAddOptionAsync(cut);

        cut.WaitForAssertion(() =>
            cut.FindAll("[data-option-row]").Should().HaveCount(1));
    }

    [Fact]
    public async Task RemoveOption_RemovesOptionRow()
    {
        await using var ctx = CreateContext();
        var cut = ctx.Render<AdminCreateQuestion>();

        await SetTypeAsync(cut, "radio");

        await InvokeAddOptionAsync(cut);
        await InvokeAddOptionAsync(cut);

        cut.WaitForAssertion(() =>
            cut.FindAll("[data-option-row]").Should().HaveCount(2));

        await InvokeRemoveOptionAsync(cut, 0);

        cut.WaitForAssertion(() =>
            cut.FindAll("[data-option-row]").Should().HaveCount(1));
    }

    [Fact]
    public async Task AddOption_MultipleRows_TrackedCorrectly()
    {
        await using var ctx = CreateContext();
        var cut = ctx.Render<AdminCreateQuestion>();

        await SetTypeAsync(cut, "multiselect");

        await InvokeAddOptionAsync(cut);
        await InvokeAddOptionAsync(cut);
        await InvokeAddOptionAsync(cut);

        cut.WaitForAssertion(() =>
            cut.FindAll("[data-option-row]").Should().HaveCount(3));
    }

    [Fact]
    public async Task SuccessAlert_Renders_When_SuccessMessage_IsSet()
    {
        await using var ctx = CreateContext();
        var cut = ctx.Render<AdminCreateQuestion>();

        await cut.InvokeAsync(() =>
        {
            typeof(AdminCreateQuestion)
                .GetField("_successMessage", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(cut.Instance, "Question 'Age' created successfully.");
            _stateHasChanged!.Invoke(cut.Instance, null);
        });

        cut.WaitForAssertion(() =>
            cut.FindComponents<MudAlert>().Should().ContainSingle(a =>
                a.Instance.Severity == Severity.Success));
    }
    [Fact]
    public async Task ErrorAlert_Renders_When_ErrorMessage_IsSet()
    {
        await using var ctx = CreateContext();
        var cut = ctx.Render<AdminCreateQuestion>();

        await cut.InvokeAsync(() =>
        {
            typeof(AdminCreateQuestion)
                .GetField("_errorMessage", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(cut.Instance, "409: A question with key 'age' already exists");
            _stateHasChanged!.Invoke(cut.Instance, null);
        });

        cut.WaitForAssertion(() =>
            cut.FindComponents<MudAlert>().Should().ContainSingle(a =>
                a.Instance.Severity == Severity.Error));
    }

    [Fact]
    public async Task SuccessAlert_DoesNotRender_When_SuccessMessage_IsNull()
    {
        await using var ctx = CreateContext();
        var cut = ctx.Render<AdminCreateQuestion>();

        cut.WaitForAssertion(() =>
            cut.FindComponents<MudAlert>()
                .Where(a => a.Instance.Severity == Severity.Success)
                .Should().BeEmpty());
    }

    [Fact]
    public async Task ErrorAlert_DoesNotRender_When_ErrorMessage_IsNull()
    {
        await using var ctx = CreateContext();
        var cut = ctx.Render<AdminCreateQuestion>();

        cut.WaitForAssertion(() =>
            cut.FindComponents<MudAlert>()
                .Where(a => a.Instance.Severity == Severity.Error)
                .Should().BeEmpty());
    }

    [Fact]
    public async Task EditMode_LoadsQuestion_AndSavesWithUpdate()
    {
        await using var ctx = CreateContext();
        var fake = ctx.Services.GetRequiredService<FakeQuestionService>();
        fake.Existing = new QuestionDefinition
        {
            Id = Guid.NewGuid(),
            Key = "age",
            Label = "Age",
            Type = "number",
            ValidationConfigs = """[{"validationType":"MinValue","minValue":0},{"validationType":"MaxValue","maxValue":120}]"""
        };

        var cut = ctx.Render<AdminCreateQuestion>(p => p.Add(x => x.Id, fake.Existing.Id));

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Edit Question"));
        var save = () => cut.FindComponents<MudButton>().Single(b => b.Markup.Contains("Save Changes"));
        cut.WaitForAssertion(() => save().Instance.Disabled.Should().BeFalse());

        await cut.InvokeAsync(() => save().Find("button").Click());

        fake.LastUpdate.Should().NotBeNull();
        fake.LastUpdate!.Key.Should().Be("age");
        fake.LastUpdate.ValidationConfigs.Should().Contain("\"maxValue\":120");
    }

    [Theory]
    [InlineData("text", "Minimum length")]
    [InlineData("long_text", "Minimum length")]
    [InlineData("number", "Minimum value")]
    [InlineData("rating", "Number of stars")]
    [InlineData("slider", "Leave empty for 100.")]
    public async Task AnswerRules_MatchTheType(string type, string field)
    {
        await using var ctx = CreateContext();
        var cut = ctx.Render<AdminCreateQuestion>();

        await SetTypeAsync(cut, type);

        cut.WaitForAssertion(() => cut.Markup.Should().Contain(field));
    }

    [Theory]
    [InlineData("email")]
    [InlineData("date")]
    [InlineData("nps")]
    [InlineData("likert")]
    public async Task AnswerRules_AreHiddenForTypesWithoutThem(string type)
    {
        await using var ctx = CreateContext();
        var cut = ctx.Render<AdminCreateQuestion>();

        await SetTypeAsync(cut, type);

        cut.WaitForAssertion(() => cut.Markup.Should().NotContain("Answer rules"));
    }

    [Fact]
    public async Task EditMode_KeepsARatingsNumberOfStars()
    {
        await using var ctx = CreateContext();
        var fake = ctx.Services.GetRequiredService<FakeQuestionService>();
        fake.Existing = new QuestionDefinition
        {
            Id = Guid.NewGuid(),
            Key = "stars",
            Label = "Rate it",
            Type = "rating",
            ValidationConfigs = """[{"validationType":"MaxValue","maxValue":10}]"""
        };

        var cut = ctx.Render<AdminCreateQuestion>(p => p.Add(x => x.Id, fake.Existing.Id));
        var save = () => cut.FindComponents<MudButton>().Single(b => b.Markup.Contains("Save Changes"));
        cut.WaitForAssertion(() => save().Instance.Disabled.Should().BeFalse());
        cut.FindComponents<MudNumericField<int?>>().Single().Find("input").GetAttribute("value").Should().Be("10");

        await cut.InvokeAsync(() => save().Find("button").Click());

        fake.LastUpdate!.ValidationConfigs.Should().Be("""[{"validationType":"MaxValue","maxValue":10}]""");
    }

    [Theory]
    [InlineData("likert", true)]
    [InlineData("radio", false)]
    [InlineData("nps", false)]
    public async Task RowsEditor_IsOnlyForLikertGrids(string type, bool shown)
    {
        await using var ctx = CreateContext();
        var cut = ctx.Render<AdminCreateQuestion>();

        await SetTypeAsync(cut, type);

        cut.WaitForAssertion(() => cut.Markup.Contains("Add Row").Should().Be(shown));
        if (shown)
        {
            cut.Markup.Should().Contain("Leave empty for Strongly disagree (1) to Strongly agree (5).");
            await cut.InvokeAsync(() => cut.FindComponents<MudButton>().Single(b => b.Markup.Contains("Add Row")).Find("button").Click());
            cut.WaitForAssertion(() => cut.FindAll("[data-row-editor]").Should().HaveCount(1));
            await cut.InvokeAsync(() => cut.Find("[data-row-editor] button[aria-label='Remove row']").Click());
            cut.WaitForAssertion(() => cut.FindAll("[data-row-editor]").Should().BeEmpty());
        }
    }

    [Fact]
    public async Task EditMode_SavesALikertGridsRows_WithTheDefaultScale()
    {
        await using var ctx = CreateContext();
        var fake = ctx.Services.GetRequiredService<FakeQuestionService>();
        fake.Existing = new QuestionDefinition
        {
            Id = Guid.NewGuid(),
            Key = "services",
            Label = "How much do you agree?",
            Type = "likert",
            Rows = [new Option { Label = "The library is great", Value = "library" }, new Option { Label = "The labs are new", Value = "labs" }]
        };

        var cut = ctx.Render<AdminCreateQuestion>(p => p.Add(x => x.Id, fake.Existing.Id));
        var save = () => cut.FindComponents<MudButton>().Single(b => b.Markup.Contains("Save Changes"));
        cut.WaitForAssertion(() => save().Instance.Disabled.Should().BeFalse("a grid without options uses the default scale"));
        cut.FindAll("[data-row-editor]").Should().HaveCount(2);

        await cut.InvokeAsync(() => save().Find("button").Click());

        fake.LastUpdate!.Rows.Select(r => (r.Value, r.Label)).Should().Equal(("library", "The library is great"), ("labs", "The labs are new"));
        fake.LastUpdate.Options.Should().BeEmpty();
    }

    [Fact]
    public async Task EditMode_ALikertGridWithoutRows_CannotBeSaved()
    {
        await using var ctx = CreateContext();
        var fake = ctx.Services.GetRequiredService<FakeQuestionService>();
        fake.Existing = new QuestionDefinition { Id = Guid.NewGuid(), Key = "services", Label = "Agree?", Type = "likert" };

        var cut = ctx.Render<AdminCreateQuestion>(p => p.Add(x => x.Id, fake.Existing.Id));

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Add at least one row."));
        cut.FindComponents<MudButton>().Single(b => b.Markup.Contains("Save Changes")).Instance.Disabled.Should().BeTrue();
    }

    [Fact]
    public async Task EditMode_KeepsASlidersRange()
    {
        await using var ctx = CreateContext();
        var fake = ctx.Services.GetRequiredService<FakeQuestionService>();
        fake.Existing = new QuestionDefinition
        {
            Id = Guid.NewGuid(),
            Key = "hours",
            Label = "Hours",
            Type = "slider",
            ValidationConfigs = """[{"validationType":"MinValue","minValue":0},{"validationType":"MaxValue","maxValue":40}]"""
        };

        var cut = ctx.Render<AdminCreateQuestion>(p => p.Add(x => x.Id, fake.Existing.Id));
        var save = () => cut.FindComponents<MudButton>().Single(b => b.Markup.Contains("Save Changes"));
        cut.WaitForAssertion(() => save().Instance.Disabled.Should().BeFalse());

        await cut.InvokeAsync(() => save().Find("button").Click());

        fake.LastUpdate!.ValidationConfigs.Should().Be(fake.Existing.ValidationConfigs);
        fake.LastUpdate.Rows.Should().BeEmpty();
    }

    [Fact]
    public async Task EditMode_RefusesAProfessorWhoDidNotCreateTheQuestion()
    {
        await using var ctx = CreateContext();
        var fake = ctx.Services.GetRequiredService<FakeQuestionService>();
        fake.Existing = new QuestionDefinition { Id = Guid.NewGuid(), Key = "first_name", Label = "First Name", Type = "text" };

        var cut = ctx.Render<AdminCreateQuestion>(p => p
            .Add(x => x.Id, fake.Existing.Id)
            .AddCascadingValue(new AdminAccess("professor", Guid.NewGuid())));

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("You can only edit questions you created."));
        cut.Markup.Should().NotContain("Save Changes");
    }

    [Fact]
    public async Task EditMode_HidesTheFormForAMissingQuestion()
    {
        await using var ctx = CreateContext();

        var cut = ctx.Render<AdminCreateQuestion>(p => p.Add(x => x.Id, Guid.NewGuid()));

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Question not found."));
        cut.FindComponents<MudForm>().Should().BeEmpty("there is nothing to save");
        cut.Markup.Should().NotContain("Save Changes");
        cut.Find("a[href='/admin/questions']").TextContent.Trim().Should().Be("Back to questions");
    }

    [Fact]
    public async Task EditMode_SaveIsDisabledWhileSaving()
    {
        await using var ctx = CreateContext();
        var fake = ctx.Services.GetRequiredService<FakeQuestionService>();
        fake.Existing = new QuestionDefinition { Id = Guid.NewGuid(), Key = "age", Label = "Age", Type = "number" };
        fake.PendingSave = new();

        var cut = ctx.Render<AdminCreateQuestion>(p => p.Add(x => x.Id, fake.Existing.Id));
        var save = () => cut.FindComponents<MudButton>().Single(b => b.Instance.OnClick.HasDelegate && b.Markup.Contains("mud-button-filled"));
        cut.WaitForAssertion(() => save().Instance.Disabled.Should().BeFalse());

        await cut.InvokeAsync(() => save().Find("button").Click());

        cut.WaitForAssertion(() => save().Instance.Disabled.Should().BeTrue());
        cut.Markup.Should().Contain("Saving...");
        fake.UpdateCallCount.Should().Be(1);

        await cut.InvokeAsync(() => fake.PendingSave.SetResult((true, null)));
        var nav = ctx.Services.GetRequiredService<NavigationManager>();
        cut.WaitForAssertion(() => nav.Uri.Should().EndWith("/admin/questions"));
        fake.UpdateCallCount.Should().Be(1);
    }

    [Theory]
    [InlineData("text", true)]
    [InlineData("number", true)]
    [InlineData("yes_no", true)]
    [InlineData("radio", true)]
    [InlineData("dropdown", true)]
    [InlineData("multiselect", true)]
    [InlineData("checkbox", false)]
    [InlineData("long_text", false)]
    [InlineData("email", false)]
    [InlineData("date", false)]
    [InlineData("rating", false)]
    [InlineData("likert", false)]
    [InlineData("nps", false)]
    [InlineData("slider", false)]
    public async Task AnswerKey_IsOnlyOfferedForGradableTypes(string type, bool shown)
    {
        await using var ctx = CreateContext();
        var cut = ctx.Render<AdminCreateQuestion>();

        await SetTypeAsync(cut, type);

        cut.WaitForAssertion(() => cut.FindAll("[data-answer-key]").Should().HaveCount(shown ? 1 : 0));
        if (shown)
        {
            cut.Markup.Should().Contain("Answer key (for quizzes)");
        }
    }

    [Fact]
    public async Task AnswerKey_ForACheckbox_AppearsOnceItHasOptions()
    {
        await using var ctx = CreateContext();
        var cut = ctx.Render<AdminCreateQuestion>();
        await SetTypeAsync(cut, "checkbox");
        cut.FindAll("[data-answer-key]").Should().BeEmpty("a single tick box has nothing to choose between");

        await InvokeAddOptionAsync(cut);
        cut.FindAll("[data-answer-key]").Should().BeEmpty("an option without a value can't be correct yet");

        var value = cut.FindComponents<MudTextField<string>>().Single(f => f.Instance.Label == "Value");
        await cut.InvokeAsync(() => value.Instance.ValueChanged.InvokeAsync("red"));

        cut.WaitForAssertion(() => cut.FindAll("[data-correct-options] input[type=checkbox]").Should().ContainSingle());
    }

    [Fact]
    public async Task EditMode_LoadsAndSavesARadioAnswerKey()
    {
        await using var ctx = CreateContext();
        var fake = ctx.Services.GetRequiredService<FakeQuestionService>();
        fake.Existing = Capital(new AnswerKey { CorrectAnswers = ["paris"], Points = 2, Explanation = "Since the tenth century." });

        var cut = await RenderEditAsync(ctx, fake.Existing);
        var correct = cut.FindComponents<MudSelect<string>>().Single(s => s.Instance.Label == "Correct answer");
        correct.Instance.GetState(x => x.Value).Should().Be("paris");
        cut.FindComponents<MudNumericField<decimal>>().Single().Instance.GetState(x => x.Value).Should().Be(2);

        await cut.InvokeAsync(() => correct.Instance.ValueChanged.InvokeAsync("lyon"));
        await SaveAsync(cut);

        fake.LastUpdate!.AnswerKey.Should().BeEquivalentTo(new AnswerKey { CorrectAnswers = ["lyon"], Points = 2, Explanation = "Since the tenth century." });
    }

    [Fact]
    public async Task EditMode_ClearingTheCorrectAnswer_RemovesTheAnswerKey()
    {
        await using var ctx = CreateContext();
        var fake = ctx.Services.GetRequiredService<FakeQuestionService>();
        fake.Existing = Capital(new AnswerKey { CorrectAnswers = ["paris"] });

        var cut = await RenderEditAsync(ctx, fake.Existing);
        var correct = cut.FindComponents<MudSelect<string>>().Single(s => s.Instance.Label == "Correct answer");
        await cut.InvokeAsync(() => correct.Instance.ValueChanged.InvokeAsync(null));
        await SaveAsync(cut);

        fake.LastUpdate!.AnswerKey.Should().BeNull();
    }

    [Fact]
    public async Task EditMode_RemovingTheCorrectOption_RemovesTheAnswerKey()
    {
        await using var ctx = CreateContext();
        var fake = ctx.Services.GetRequiredService<FakeQuestionService>();
        fake.Existing = Capital(new AnswerKey { CorrectAnswers = ["lyon"] });

        var cut = await RenderEditAsync(ctx, fake.Existing);
        await InvokeRemoveOptionAsync(cut, 1);
        await SaveAsync(cut);

        fake.LastUpdate!.Options.Select(o => o.Value).Should().Equal("paris");
        fake.LastUpdate.AnswerKey.Should().BeNull();
    }

    [Fact]
    public async Task EditMode_ChangingToATypeThatIsntGraded_RemovesTheAnswerKey()
    {
        await using var ctx = CreateContext();
        var fake = ctx.Services.GetRequiredService<FakeQuestionService>();
        fake.Existing = Capital(new AnswerKey { CorrectAnswers = ["paris"] });

        var cut = await RenderEditAsync(ctx, fake.Existing);
        await SetTypeAsync(cut, "long_text");
        await SaveAsync(cut);

        fake.LastUpdate!.AnswerKey.Should().BeNull();
    }

    [Fact]
    public async Task EditMode_SavesTheSetOfCorrectOptions_InOptionOrder()
    {
        await using var ctx = CreateContext();
        var fake = ctx.Services.GetRequiredService<FakeQuestionService>();
        fake.Existing = new QuestionDefinition
        {
            Id = Guid.NewGuid(),
            Key = "primes",
            Label = "Which are prime?",
            Type = "multiselect",
            Options = [new() { Value = "2", Label = "Two" }, new() { Value = "3", Label = "Three" }, new() { Value = "4", Label = "Four" }],
            AnswerKey = new AnswerKey { CorrectAnswers = ["3"] },
        };

        var cut = await RenderEditAsync(ctx, fake.Existing);
        var boxes = cut.FindComponents<MudCheckBox<bool>>().Where(c => c.FindAll("[data-correct-options]").Count == 0).ToList();
        boxes.Select(b => b.Instance.GetState(x => x.Value)).Should().Equal(false, true, false);
        cut.Markup.Should().Contain("each wrong one takes a share away");

        await cut.InvokeAsync(() => boxes[0].Instance.ValueChanged.InvokeAsync(true));
        await SaveAsync(cut);

        fake.LastUpdate!.AnswerKey!.CorrectAnswers.Should().Equal("2", "3");
    }

    [Fact]
    public async Task EditMode_SavesYesNoNumberAndTextAnswerKeys()
    {
        foreach (var (type, correct, expected) in new (string, string[], string[])[]
        {
            ("yes_no", ["false"], ["false"]),
            ("number", ["4.5"], ["4.5"]),
            ("text", ["Paris", "City of Light"], ["Paris", "City of Light"]),
        })
        {
            await using var ctx = CreateContext();
            var fake = ctx.Services.GetRequiredService<FakeQuestionService>();
            fake.Existing = new QuestionDefinition
            {
                Id = Guid.NewGuid(),
                Key = "graded",
                Label = "Graded",
                Type = type,
                AnswerKey = new AnswerKey { CorrectAnswers = correct.ToList(), Points = 3 },
            };

            var cut = await RenderEditAsync(ctx, fake.Existing);
            await SaveAsync(cut);

            fake.LastUpdate!.AnswerKey!.CorrectAnswers.Should().Equal(expected, type);
            fake.LastUpdate.AnswerKey.Points.Should().Be(3);
        }
    }

    [Fact]
    public async Task AcceptedAnswers_AreOnePerLine_TrimmedAndWithoutBlanks()
    {
        await using var ctx = CreateContext();
        var fake = ctx.Services.GetRequiredService<FakeQuestionService>();
        fake.Existing = new QuestionDefinition { Id = Guid.NewGuid(), Key = "capital_name", Label = "Capital?", Type = "text" };

        var cut = await RenderEditAsync(ctx, fake.Existing);
        var accepted = cut.FindComponents<MudTextField<string>>().Single(f => f.Instance.Label == "Accepted answers");
        await cut.InvokeAsync(() => accepted.Instance.ValueChanged.InvokeAsync("  Paris \n\n paris \nParis"));
        await SaveAsync(cut);

        fake.LastUpdate!.AnswerKey!.CorrectAnswers.Should().Equal("Paris", "paris");
    }

    private static QuestionDefinition Capital(AnswerKey key) => new()
    {
        Id = Guid.NewGuid(),
        Key = "capital",
        Label = "Capital of France?",
        Type = "radio",
        Options = [new() { Value = "paris", Label = "Paris" }, new() { Value = "lyon", Label = "Lyon" }],
        AnswerKey = key,
    };

    private static Task<IRenderedComponent<AdminCreateQuestion>> RenderEditAsync(BunitContext ctx, QuestionDefinition existing)
    {
        var cut = ctx.Render<AdminCreateQuestion>(p => p.Add(x => x.Id, existing.Id));
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Save Changes"));
        return Task.FromResult(cut);
    }

    private static async Task SaveAsync(IRenderedComponent<AdminCreateQuestion> cut)
    {
        var save = cut.FindComponents<MudButton>().Single(b => b.Markup.Contains("Save Changes"));
        await cut.InvokeAsync(() => save.Find("button").Click());
    }

    private static BunitContext CreateContext()
    {
        var ctx = new BunitContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        ctx.Services.AddMudServices();

        var fake = new FakeQuestionService();
        ctx.Services.AddSingleton<FakeQuestionService>(fake);
        ctx.Services.AddSingleton<IQuestionService>(fake);


        ctx.Render<MudThemeProvider>();
        ctx.Render<MudPopoverProvider>();
        ctx.Render<MudDialogProvider>();
        ctx.Render<MudSnackbarProvider>();

        return ctx;
    }

    private static readonly MethodInfo? _stateHasChanged =
        typeof(ComponentBase).GetMethod("StateHasChanged", BindingFlags.Instance | BindingFlags.NonPublic);

    private static async Task SetTypeAsync(IRenderedComponent<AdminCreateQuestion> cut, string type)
    {
        var typeField = typeof(AdminCreateQuestion)
            .GetField("_newQuestion", BindingFlags.Instance | BindingFlags.NonPublic);

        typeField.Should().NotBeNull();

        await cut.InvokeAsync(() =>
        {
            var question = typeField!.GetValue(cut.Instance);
            question!.GetType().GetProperty("Type")!.SetValue(question, type);
            _stateHasChanged!.Invoke(cut.Instance, null);
        });
    }

    private static async Task InvokeAddOptionAsync(IRenderedComponent<AdminCreateQuestion> cut)
    {
        var method = typeof(AdminCreateQuestion).GetMethod(
            "AddOption",
            BindingFlags.Instance | BindingFlags.NonPublic);

        method.Should().NotBeNull();

        await cut.InvokeAsync(() =>
        {
            method!.Invoke(cut.Instance, null);
            _stateHasChanged!.Invoke(cut.Instance, null);
        });
    }

    private static async Task InvokeRemoveOptionAsync(IRenderedComponent<AdminCreateQuestion> cut, int index)
    {
        var method = typeof(AdminCreateQuestion).GetMethod(
            "RemoveOption",
            BindingFlags.Instance | BindingFlags.NonPublic);

        method.Should().NotBeNull();

        await cut.InvokeAsync(() =>
        {
            method!.Invoke(cut.Instance, new object[] { index });
            _stateHasChanged!.Invoke(cut.Instance, null);
        });
    }

    private static async Task InvokeSaveQuestionAsync(IRenderedComponent<AdminCreateQuestion> cut)
    {
        var method = typeof(AdminCreateQuestion).GetMethod(
            "SaveQuestionAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);

        method.Should().NotBeNull();

        await cut.InvokeAsync(async () =>
        {
            var task = method!.Invoke(cut.Instance, null) as Task;
            task.Should().NotBeNull();
            await task!;
        });
    }
}
