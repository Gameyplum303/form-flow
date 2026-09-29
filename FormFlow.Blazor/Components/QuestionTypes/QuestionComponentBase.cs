using FormFlow.Data.Models;
using Microsoft.AspNetCore.Components;

namespace FormFlow.Blazor.Components.QuestionTypes;

/// <summary>
/// Base for every question type component. A component can be used on its own (it keeps
/// its answer internally, as in the admin preview) or bound by a parent form through
/// <see cref="Value"/> and <see cref="ValueChanged"/>.
/// </summary>
public abstract class QuestionComponentBase : ComponentBase
{
    [Parameter, EditorRequired]
    public QuestionDefinition Question { get; set; } = default!;

    /// <summary>
    /// The current answer. Single-value questions use the first entry; checkbox and
    /// multiselect questions hold one entry per selected option.
    /// </summary>
    [Parameter]
    public IReadOnlyList<string>? Value { get; set; }

    [Parameter]
    public EventCallback<IReadOnlyList<string>> ValueChanged { get; set; }

    /// <summary>A validation message to show under the question.</summary>
    [Parameter]
    public string? Error { get; set; }

    protected bool HasError => !string.IsNullOrWhiteSpace(Error);

    /// <summary>A unique id for the question's input, so its label can point at it.</summary>
    protected string InputId { get; } = $"question-{Guid.NewGuid():N}";

    /// <summary>The bound single value, if a parent supplied one.</summary>
    protected string? BoundValue => Value is { Count: > 0 } ? Value[0] : null;

    /// <summary>Tells the parent form about a new answer.</summary>
    protected Task ReportAsync(IEnumerable<string?> values) =>
        ValueChanged.InvokeAsync(values.Where(v => !string.IsNullOrEmpty(v)).Select(v => v!).ToList());

    protected Task ReportAsync(string? value) => ReportAsync([value]);

    /// <summary>
    /// Ticks or unticks one option of a question that takes several answers, then reports the
    /// selection in option order, so stored answers are stable.
    /// </summary>
    protected Task ToggleOptionAsync(ISet<string> selected, string value, bool isChecked)
    {
        if (isChecked)
        {
            selected.Add(value);
        }
        else
        {
            selected.Remove(value);
        }

        return ReportAsync(Question.Options.Select(o => o.Value).Where(selected.Contains));
    }
}
