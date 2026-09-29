using Bunit;
using FluentAssertions;
using FormFlow.Blazor.Components;
using FormFlow.Data.Models;
using MudBlazor;
using MudBlazor.Services;

namespace FormFlow.Blazor.Tests.Components;

/// <summary>
/// Question components report answers to a parent form through Value/ValueChanged.
/// </summary>
public class QuestionBindingTests
{
    private static BunitContext CreateContext()
    {
        var ctx = new BunitContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        ctx.Services.AddMudServices();
        ctx.Render<MudPopoverProvider>();
        return ctx;
    }

    private IReadOnlyList<string>? _reported;

    private IRenderedComponent<QuestionRenderer> RenderBound(BunitContext ctx, QuestionDefinition question, params string[] value) =>
        ctx.Render<QuestionRenderer>(p => p
            .Add(x => x.Question, question)
            .Add(x => x.Value, value)
            .Add(x => x.ValueChanged, v => _reported = v));

    private static QuestionDefinition Q(string type, params string[] options) => new()
    {
        Id = Guid.NewGuid(),
        Key = "q",
        Label = "Question",
        Type = type,
        Options = options.Select(o => new Option { Label = o.ToUpperInvariant(), Value = o }).ToList()
    };

    [Fact]
    public async Task Radio_ReportsSelectedOption()
    {
        await using var ctx = CreateContext();
        var cut = RenderBound(ctx, Q("radio", "a", "b"));

        await cut.InvokeAsync(() => cut.FindComponent<MudRadioGroup<string>>().Instance.ValueChanged.InvokeAsync("b"));

        _reported.Should().Equal("b");
    }

    [Fact]
    public async Task Multiselect_ReportsSelectionsInOptionOrder()
    {
        await using var ctx = CreateContext();
        var cut = RenderBound(ctx, Q("multiselect", "a", "b", "c"));
        var boxes = cut.FindComponents<MudCheckBox<bool>>();

        await cut.InvokeAsync(() => boxes[2].Instance.ValueChanged.InvokeAsync(true));
        await cut.InvokeAsync(() => boxes[0].Instance.ValueChanged.InvokeAsync(true));

        _reported.Should().Equal("a", "c");
    }

    [Fact]
    public async Task CheckboxWithoutOptions_ReportsTrueOrFalse()
    {
        await using var ctx = CreateContext();
        var cut = RenderBound(ctx, Q("checkbox"));

        await cut.InvokeAsync(() => cut.FindComponent<MudCheckBox<bool>>().Instance.ValueChanged.InvokeAsync(true));

        _reported.Should().Equal("true");
    }

    [Fact]
    public async Task BoundValue_IsShownAndErrorIsDisplayed()
    {
        await using var ctx = CreateContext();
        var cut = ctx.Render<QuestionRenderer>(p => p
            .Add(x => x.Question, Q("yes_no"))
            .Add(x => x.Value, ["false"])
            .Add(x => x.ValueChanged, _ => { })
            .Add(x => x.Error, "Pick one"));

        cut.FindComponent<MudRadioGroup<bool?>>().Instance.Value.Should().BeFalse();
        cut.Markup.Should().Contain("Pick one");
    }
}
