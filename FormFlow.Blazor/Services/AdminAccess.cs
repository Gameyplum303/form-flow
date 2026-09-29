using FormFlow.Data.Models;

namespace FormFlow.Blazor.Services
{
    /// <summary>
    /// What the admin pages show for the current account (or the role an administrator is previewing):
    /// administrators manage everything, professors manage what they created.
    /// </summary>
    public record AdminAccess(string Role, Guid? UserId)
    {
        /// <summary>Used when a page renders without the guard, as in component tests.</summary>
        public static readonly AdminAccess Administrator = new(Roles.Admin, null);

        public bool IsAdmin => Role == Roles.Admin;

        public bool CanBuild => Roles.CanBuild(Role);

        public bool CanManage(IOwned item) =>
            IsAdmin || (Role == Roles.Professor && item.OwnerId is { } owner && owner == UserId);
    }
}
