using Microsoft.AspNetCore.Components;

namespace FormFlow.Blazor.Services
{
    public static class NavigationExtensions
    {
        /// <summary>Whether the current page is in the admin area: /admin or anything under it.</summary>
        public static bool IsAdminArea(this NavigationManager nav)
        {
            var path = nav.ToBaseRelativePath(nav.Uri);
            return path == "admin" || path.StartsWith("admin/") || path.StartsWith("admin?");
        }
    }
}
