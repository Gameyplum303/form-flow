namespace FormFlow.Data.Models
{
    /// <summary>One choice of a dropdown, radio, checkbox or multiselect question, or a row or column of a likert grid.</summary>
    public class Option
    {
        public required string Label { get; set; }

        public required string Value { get; set; }
    }
}