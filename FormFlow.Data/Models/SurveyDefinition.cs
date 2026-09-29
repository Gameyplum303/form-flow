using LiteDB;

namespace FormFlow.Data.Models
{
    public class SurveyDefinition : IOwned
    {
        [BsonId]
        public Guid Id { get; set; }

        public required string Title { get; set; }
        public required string Description { get; set; }
        public required List<Guid> QuestionIds { get; set; }
        public required DateTime CreatedAt { get; set; }
        public Guid? OwnerId { get; set; }
        public string? OwnerName { get; set; }
        //public int? Version { get; set; }
    }
}