using FormFlow.Blazor.Components.QuestionTypes;
using Types = FormFlow.Data.Models.QuestionTypes;

namespace FormFlow.Blazor.Components;

/// <summary>Picks the component that draws a question of the given type.</summary>
public static class QuestionComponentMapper
{
    public static Type? Resolve(string? type) =>
        type?.ToLowerInvariant() switch
        {
            Types.Dropdown => typeof(DropdownQuestion),
            Types.Text => typeof(TextQuestion),
            Types.YesNo => typeof(YesNoQuestion),
            Types.Number => typeof(NumberQuestion),
            Types.Multiselect => typeof(MultiselectQuestion),
            Types.Checkbox => typeof(CheckboxQuestion),
            Types.Radio => typeof(RadioQuestion),
            Types.LongText => typeof(LongTextQuestion),
            Types.Email => typeof(EmailQuestion),
            Types.Date => typeof(DateQuestion),
            Types.Rating => typeof(RatingQuestion),
            _ => null
        };
}
