namespace FormFlow.Blazor.Services
{
    /// <summary>
    /// What the signed-in account may do, cascaded to the admin pages by AdminGuard so they can
    /// hide actions a view-only account isn't allowed to take. The API enforces the same rule.
    /// </summary>
    public record AdminAccess(bool CanEdit);
}
