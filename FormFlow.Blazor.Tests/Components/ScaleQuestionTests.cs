using Bunit;
using FluentAssertions;
using FormFlow.Blazor.Components;
using FormFlow.Blazor.Components.QuestionTypes;
using FormFlow.Data.Models;
using MudBlazor;
using MudBlazor.Services;

namespace FormFlow.Blazor.Tests.Components;

/// <summary>The likert grid, NPS and slider components, on their own and bound to a form.</summary>
public class ScaleQuestionTests
{
    private IReadOnlyList<string>? _reported;

    private static BunitContext CreateContext()
    {
        var ctx = new BunitContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        ctx.Services.AddMudServices();
        return ctx;
    }

    private IRenderedComponent<QuestionRenderer> RenderBound(BunitContext ctx, QuestionDefinition question, params string[] value) =>
        ctx.Render<QuestionRenderer>(p => p
            .Add(x => x.Question, question)
            .Add(x => x.Value, value)
            .Add(x => x.ValueChanged, v => _reported = v));

    private static QuestionDefinition Likert(params string[] scale) => new()
    {
        Id = Guid.NewGuid(),
        Key = "services",
        Label = "How much do you agree?",
        Type = QuestionTypes.Likert,
        Options = scale.Select(o => new Option { Label = o.ToUpperInvariant(), Value = o }).ToList(),
        Rows =
        [
            new Option { Label = "The library has what I need", Value = "library" },
            new Option { Label = "The labs are up to date", Value = "labs" },
        ]
    };

    private static QuestionDefinition Q(string type, string? rules = null) => new()
    {
        Id = Guid.NewGuid(),
        Key = "q",
        Label = "How likely are you to recommend us?",
        Type = type,
        ValidationConfigs = rules
    };

    [Fact]
    public async Task Likert_MakesEachStatementARadioGroupLabelledByIt()
    {
        await using var ctx = CreateContext();
        var cut = ctx.Render<LikertQuestion>(p => p.Add(x => x.Question, Likert()));

        var groups = cut.FindAll("[role=radiogroup]");
        groups.Should().HaveCount(2);
        foreach (var (group, statement) in groups.Zip(new[] { "The library has what I need", "The labs are up to date" }))
        {
            cut.Find($"#{group.GetAttribute("aria-labelledby")}").TextContent.Should().Be(statement);
            group.QuerySelectorAll("input[type=radio]").Select(r => r.GetAttribute("aria-label"))
                .Should().Equal("Strongly disagree", "Disagree", "Neutral", "Agree", "Strongly agree");
        }
        cut.Find(".likert-head").GetAttribute("aria-hidden").Should().Be("true", "each radio button already carries its column's label");
        cut.FindAll(".likert-head .likert-column").Select(c => c.TextContent).Should().HaveCount(5);
    }

    [Fact]
    public async Task Likert_UsesTheQuestionsOptionsAsTheScale()
    {
        await using var ctx = CreateContext();
        var cut = ctx.Render<LikertQuestion>(p => p.Add(x => x.Question, Likert("never", "often")));

        cut.FindAll(".likert-head .likert-column").Select(c => c.TextContent).Should().Equal("NEVER", "OFTEN");
        cut.FindAll("input[type=radio]").Should().HaveCount(4);
    }

    [Fact]
    public async Task Likert_ShowsTheBoundAnswers_AndReportsRowsInOrder()
    {
        await using var ctx = CreateContext();
        var cut = RenderBound(ctx, Likert(), "labs=2");
        var groups = cut.FindComponents<MudRadioGroup<string>>();

        groups[0].Instance.Value.Should().BeNull();
        groups[1].Instance.Value.Should().Be("2");

        await cut.InvokeAsync(() => groups[0].Instance.ValueChanged.InvokeAsync("5"));

        _reported.Should().Equal("library=5", "labs=2");
    }

    [Fact]
    public async Task Nps_ShowsElevenButtonsWithLabelledEnds_AndReportsTheScore()
    {
        await using var ctx = CreateContext();
        var cut = RenderBound(ctx, Q(QuestionTypes.Nps));

        var buttons = cut.FindAll(".nps-scale button");
        buttons.Select(b => b.TextContent.Trim()).Should().Equal(Enumerable.Range(0, 11).Select(n => n.ToString()));
        buttons[0].GetAttribute("aria-label").Should().Be("0, not likely");
        buttons[10].GetAttribute("aria-label").Should().Be("10, extremely likely");
        cut.Find(".nps-scale").GetAttribute("role").Should().Be("group");
        cut.Find(".scale-ends").TextContent.Should().Contain("Not likely").And.Contain("Extremely likely");

        await cut.InvokeAsync(() => cut.FindAll(".nps-scale button")[7].Click());

        _reported.Should().Equal("7");
    }

    [Fact]
    public async Task Nps_MarksTheChosenButtonPressed_AndPressingItAgainClearsIt()
    {
        await using var ctx = CreateContext();
        var cut = ctx.Render<NpsQuestion>(p => p
            .Add(x => x.Question, Q(QuestionTypes.Nps))
            .Add(x => x.Value, ["9"])
            .Add(x => x.ValueChanged, v => _reported = v));

        cut.FindAll(".nps-scale button[aria-pressed=true]").Single().TextContent.Trim().Should().Be("9");

        await cut.InvokeAsync(() => cut.FindAll(".nps-scale button")[9].Click());

        _reported.Should().BeEmpty();
    }

    [Fact]
    public async Task Slider_IsUnansweredUntilMoved()
    {
        await using var ctx = CreateContext();
        var cut = RenderBound(ctx, Q(QuestionTypes.Slider));
        var slider = cut.FindComponent<MudSlider<int>>();

        slider.Instance.Min.Should().Be(0);
        slider.Instance.Max.Should().Be(100);
        cut.Find("[data-slider-value]").TextContent.Should().Be("–");
        cut.Markup.Should().Contain("Move the slider to answer");
        cut.Find("input[type=range]").GetAttribute("aria-valuetext").Should().Be("Not answered");

        await cut.InvokeAsync(() => slider.Instance.ValueChanged.InvokeAsync(64));

        _reported.Should().Equal("64");
    }

    [Fact]
    public async Task Slider_UsesTheQuestionsRange_AndShowsTheBoundValue()
    {
        await using var ctx = CreateContext();
        var question = Q(QuestionTypes.Slider, """[{"validationType":"MinValue","minValue":1},{"validationType":"MaxValue","maxValue":7}]""");
        var cut = RenderBound(ctx, question, "5");
        var slider = cut.FindComponent<MudSlider<int>>();

        slider.Instance.Min.Should().Be(1);
        slider.Instance.Max.Should().Be(7);
        cut.Find("input[type=range]").GetAttribute("value").Should().Be("5");
        cut.Find("[data-slider-value]").TextContent.Should().Be("5");
        cut.Markup.Should().NotContain("Move the slider to answer");

        var label = cut.Find("label.question-label");
        cut.Find("input[type=range]").Id.Should().Be(label.GetAttribute("for"), "the question's label names the slider");
    }
}
