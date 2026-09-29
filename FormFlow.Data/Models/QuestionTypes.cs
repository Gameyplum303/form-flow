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

        public static readonly IReadOnlyList<string> All =
            [Text, Number, YesNo, Dropdown, Radio, Checkbox, Multiselect, LongText, Email, Date, Rating];

        /// <summary>Types whose answers must come from the question's options.</summary>
        public static readonly IReadOnlyList<string> Choice = [Dropdown, Radio, Checkbox, Multiselect];

        /// <summary>Types that accept more than one selected option.</summary>
        public static readonly IReadOnlyList<string> MultiValue = [Checkbox, Multiselect];

        /// <summary>Types whose answers can have minimum and maximum length rules.</summary>
        public static readonly IReadOnlyList<string> LengthRules = [Text, LongText];

        /// <summary>A rating runs from 1 to this many stars unless the question sets a maximum value.</summary>
        public const int DefaultRatingScale = 5;

        /// <summary>The most stars a rating question can have.</summary>
        public const int MaxRatingScale = 10;

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
    }
}
