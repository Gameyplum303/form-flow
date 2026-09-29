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

        // Sharing. Surveys stored before sharing existed were open to everyone and on the public list,
        // so that is what a missing value means; new surveys start as unlisted drafts.

        /// <summary>One of <see cref="SurveyStatuses"/>. Only published surveys can be opened and answered.</summary>
        public string Status { get; set; } = SurveyStatuses.Published;

        /// <summary>Whether a published survey appears on the public list. Unlisted ones are reached by their link.</summary>
        public bool Listed { get; set; } = true;

        /// <summary>The short code in the survey's share link, /s/{code}.</summary>
        public string? ShareCode { get; set; }

        /// <summary>When the survey stops taking answers (UTC), or null to stay open.</summary>
        public DateTime? ClosesAt { get; set; }

        public bool IsPublished() => Status == SurveyStatuses.Published;

        public bool IsClosed(DateTime utcNow) => ClosesAt is { } closesAt && closesAt <= utcNow;

        /// <summary>Whether anyone can find it on the public list right now.</summary>
        public bool IsOnPublicList(DateTime utcNow) => IsPublished() && Listed && !IsClosed(utcNow);
    }

    public static class SurveyStatuses
    {
        /// <summary>Only the people who manage the survey can open it.</summary>
        public const string Draft = "draft";

        /// <summary>Anyone with the link can answer it, until it closes.</summary>
        public const string Published = "published";

        public static bool IsKnown(string? status) => status is Draft or Published;
    }

    /// <summary>Body of PUT /api/surveys/{id}/sharing.</summary>
    public class SurveySharing
    {
        public string Status { get; set; } = SurveyStatuses.Draft;
        public bool Listed { get; set; }

        /// <summary>When the survey stops taking answers, or null to stay open.</summary>
        public DateTime? ClosesAt { get; set; }
    }
}
