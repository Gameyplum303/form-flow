using FormFlow.Data.Services;

namespace FormFlow.Data.Models
{
    /// <summary>
    /// The question type strings the system understands.
    /// </summary>
    public static class QuestionTypes
    {
        public const string Text = "text";
        public const string Number = "number";
        public const string YesNo = "yes_no";
        public const string Dropdown = "dropdown";
        public const string Radio = "radio";
        public const string Checkbox = "checkbox";
        public const string Multiselect = "multiselect";
        public const string LongText = "long_text";
        public const string Email = "email";
        public const string Date = "date";
        public const string Rating = "rating";
        public const string Likert = "likert";
        public const string Nps = "nps";
        public const string Slider = "slider";

        public static readonly IReadOnlyList<string> All =
            [Text, Number, YesNo, Dropdown, Radio, Checkbox, Multiselect, LongText, Email, Date, Rating, Likert, Nps, Slider];

        /// <summary>Types whose answers must come from the question's options.</summary>
        public static readonly IReadOnlyList<string> Choice = [Dropdown, Radio, Checkbox, Multiselect];

        /// <summary>Types whose answer can hold more than one value: selected options, or one per likert row.</summary>
        public static readonly IReadOnlyList<string> MultiValue = [Checkbox, Multiselect, Likert];

        /// <summary>Types whose answers can have minimum and maximum length rules.</summary>
        public static readonly IReadOnlyList<string> LengthRules = [Text, LongText];

        /// <summary>A rating runs from 1 to this many stars unless the question sets a maximum value.</summary>
        public const int DefaultRatingScale = 5;

        /// <summary>The most stars a rating question can have.</summary>
        public const int MaxRatingScale = 10;

        /// <summary>A slider runs from 0 to 100 unless the question sets a minimum or maximum value.</summary>
        public const int DefaultSliderMin = 0;
        public const int DefaultSliderMax = 100;

        /// <summary>An NPS answer is a whole number from 0 to this.</summary>
        public const int NpsMax = 10;

        /// <summary>NPS answers from 0 to this are detractors; 9 and 10 are promoters, and the rest passives.</summary>
        public const int NpsDetractorMax = 6;
        public const int NpsPromoterMin = 9;

        /// <summary>
        /// The scale a likert grid uses when its question has no options: five points from
        /// "Strongly disagree" (1) to "Strongly agree" (5).
        /// </summary>
        public static readonly IReadOnlyList<Option> DefaultLikertScale =
        [
            new Option { Value = "1", Label = "Strongly disagree" },
            new Option { Value = "2", Label = "Disagree" },
            new Option { Value = "3", Label = "Neutral" },
            new Option { Value = "4", Label = "Agree" },
            new Option { Value = "5", Label = "Strongly agree" },
        ];

        /// <summary>Separates the row from the chosen option in a likert answer, as in "library=4".</summary>
        public const char LikertSeparator = '=';

        /// <summary>The name people see for a type, such as "Yes/No" for yes_no. Unknown types show as they are.</summary>
        public static string DisplayName(string? type) => type?.ToLowerInvariant() switch
        {
            Text => "Text",
            Number => "Number",
            YesNo => "Yes/No",
            Dropdown => "Dropdown",
            Radio => "Radio",
            Checkbox => "Checkbox",
            Multiselect => "Multiselect",
            LongText => "Long text",
            Email => "Email",
            Date => "Date",
            Rating => "Rating",
            Likert => "Likert grid",
            Nps => "NPS (0–10)",
            Slider => "Slider",
            _ => type ?? "",
        };

        public static bool IsKnown(string? type) => type is not null && All.Contains(type.ToLowerInvariant());

        public static bool IsChoice(string? type) => type is not null && Choice.Contains(type.ToLowerInvariant());

        public static bool IsMultiValue(string? type) => type is not null && MultiValue.Contains(type.ToLowerInvariant());

        public static bool HasLengthRules(string? type) => type is not null && LengthRules.Contains(type.ToLowerInvariant());

        /// <summary>
        /// The number of stars a rating question shows: its MaxValue rule when it has one
        /// (between 2 and <see cref="MaxRatingScale"/>), otherwise <see cref="DefaultRatingScale"/>.
        /// </summary>
        public static int RatingScale(QuestionDefinition question)
        {
            var max = ValidationRules.MaxValue(question.ValidationConfigs);
            return max is { } m && m >= 2 && m <= MaxRatingScale && m == Math.Floor(m) ? (int)m : DefaultRatingScale;
        }

        /// <summary>The columns of a likert grid: the question's options, or <see cref="DefaultLikertScale"/> when it has none.</summary>
        public static IReadOnlyList<Option> LikertScale(QuestionDefinition question) =>
            question.Options is { Count: > 0 } options ? options : DefaultLikertScale;

        /// <summary>
        /// The lowest and highest values of a slider: its MinValue and MaxValue rules (or a Range rule)
        /// when set, otherwise <see cref="DefaultSliderMin"/> and <see cref="DefaultSliderMax"/>.
        /// </summary>
        public static (decimal Min, decimal Max) SliderRange(QuestionDefinition question) =>
            (ValidationRules.MinValue(question.ValidationConfigs) ?? DefaultSliderMin,
             ValidationRules.MaxValue(question.ValidationConfigs) ?? DefaultSliderMax);

        /// <summary>A likert answer for one row, as stored: "rowValue=optionValue".</summary>
        public static string LikertAnswer(string row, string option) => $"{row}{LikertSeparator}{option}";

        /// <summary>Splits a stored likert answer into its row and option; false when it has no separator.</summary>
        public static bool TryParseLikertAnswer(string? answer, out string row, out string option)
        {
            var at = answer?.IndexOf(LikertSeparator) ?? -1;
            row = at > 0 ? answer![..at] : string.Empty;
            option = at > 0 ? answer![(at + 1)..] : string.Empty;
            return at > 0;
        }
    }
}
