using Microsoft.AspNetCore.Components;

namespace FormFlow.Blazor.Services
{
    public static class NavigationExtensions
    {
        /// <summary>Whether the current page is in the admin area: /admin or anything under it.</summary>
        public static bool IsAdminArea(this NavigationManager nav) => IsAdminPath(nav.ToBaseRelativePath(nav.Uri));

        /// <summary>Whether a site path, such as "/admin/surveys" or "admin?x=1", is in the admin area.</summary>
        public static bool IsAdminPath(string path)
        {
            if (path.StartsWith('/'))
            {
                path = path[1..];
            }
            return path == "admin" || path.StartsWith("admin/") || path.StartsWith("admin?");
        }
    }
}
