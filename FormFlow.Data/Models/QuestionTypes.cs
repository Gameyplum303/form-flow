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

        public static readonly IReadOnlyList<string> All =
            [Text, Number, YesNo, Dropdown, Radio, Checkbox, Multiselect];

        /// <summary>Types whose answers must come from the question's options.</summary>
        public static readonly IReadOnlyList<string> Choice = [Dropdown, Radio, Checkbox, Multiselect];

        /// <summary>Types that accept more than one selected option.</summary>
        public static readonly IReadOnlyList<string> MultiValue = [Checkbox, Multiselect];

        public static bool IsKnown(string? type) => type is not null && All.Contains(type.ToLowerInvariant());

        public static bool IsChoice(string? type) => type is not null && Choice.Contains(type.ToLowerInvariant());

        public static bool IsMultiValue(string? type) => type is not null && MultiValue.Contains(type.ToLowerInvariant());
    }
}
