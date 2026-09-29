using FormFlow.Data.Models;

namespace FormFlow.Blazor.Services
{
    public static class OwnedExtensions
    {
        /// <summary>Who created a survey or question. The seeded demo content has no owner and belongs to the administrators.</summary>
        public static string OwnerLabel(this IOwned item) => item.OwnerName ?? "Administrators";
    }
}
