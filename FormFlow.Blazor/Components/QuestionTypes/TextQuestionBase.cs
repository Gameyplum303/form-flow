namespace FormFlow.Blazor.Components.QuestionTypes;

/// <summary>
/// Shared state for questions answered by typing one value: text, long text, email, number and date.
/// </summary>
public abstract class TextQuestionBase : QuestionComponentBase
{
    /// <summary>What the input shows: the bound answer, or the question's default before anything is typed.</summary>
    protected string? CurrentValue { get; private set; }

    protected override void OnParametersSet()
    {
        if (Value is not null)
        {
            CurrentValue = BoundValue;
        }
        else if (CurrentValue is null && !string.IsNullOrWhiteSpace(Question.DefaultValue))
        {
            CurrentValue = Question.DefaultValue;
        }
    }

    protected Task OnChangedAsync(string? value)
    {
        CurrentValue = value;
        return ReportAsync(value);
    }
}
