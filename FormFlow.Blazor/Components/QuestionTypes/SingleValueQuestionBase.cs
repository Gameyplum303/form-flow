namespace FormFlow.Blazor.Components.QuestionTypes;

/// <summary>
/// Shared state for questions answered with one value: typed (text, long text, email, number and date)
/// or picked from the options (dropdown and radio).
/// </summary>
public abstract class SingleValueQuestionBase : QuestionComponentBase
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
