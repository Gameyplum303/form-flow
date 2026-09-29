namespace FormFlow.Data.Models
{
    /// <summary>Body of POST and PUT /api/questions: a question without its id or owner.</summary>
    public class NewQuestion
    {
        public string Label { get; set; } = string.Empty;
        public string Key { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public bool Required { get; set; } = true;
        public string? Placeholder { get; set; }
        public string? DefaultValue { get; set; }
        public string? HelpText { get; set; }
        public List<Option> Options { get; set; } = new();

        /// <summary>The statements of a likert grid; empty for other types.</summary>
        public List<Option> Rows { get; set; } = new();
        public VisibleIf? VisibleIf { get; set; }
        public string? ValidationConfigs { get; set; }
    }
}
