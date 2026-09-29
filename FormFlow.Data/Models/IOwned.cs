namespace FormFlow.Data.Models
{
    /// <summary>Something an account created and can manage: a survey or a question.</summary>
    public interface IOwned
    {
        /// <summary>The account that created it, or null for the seeded demo data, which administrators manage.</summary>
        Guid? OwnerId { get; set; }

        /// <summary>That account's username, shown in the admin pages.</summary>
        string? OwnerName { get; set; }
    }
}
