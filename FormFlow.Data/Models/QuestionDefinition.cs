using LiteDB;

namespace FormFlow.Data.Models
{
    /// <summary>A question in the question bank, which surveys list by id and answers refer to by key.</summary>
    public class QuestionDefinition : IOwned
    {
        [BsonId]
        public Guid Id { get; set; }

        public Guid? OwnerId { get; set; }
        public string? OwnerName { get; set; }

        public required string Key { get; set; }

        public required string Label { get; set; }

        public required string Type { get; set; }

        public bool Required { get; set; } = true;

        public string? Placeholder { get; set; }

        public string? DefaultValue { get; set; }

        public List<Option> Options { get; set; } = new();

        public VisibleIf? VisibleIf { get; set; }

        public string? ValidationConfigs { get; set; }

        public string? HelpText { get; set; }
    }
}
