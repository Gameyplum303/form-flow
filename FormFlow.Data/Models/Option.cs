namespace FormFlow.Data.Models
{
    /// <summary>One choice of a dropdown, radio, checkbox or multiselect question.</summary>
    public class Option
    {
        public required string Label { get; set; }

        public required string Value { get; set; }
    }
}