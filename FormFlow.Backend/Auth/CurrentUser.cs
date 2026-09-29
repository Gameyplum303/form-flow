using System.Security.Claims;
using FormFlow.Data.Models;
using Microsoft.IdentityModel.JsonWebTokens;

namespace FormFlow.Backend.Auth
{
    /// <summary>The signed-in account behind a request, read from its token.</summary>
    public record CurrentUser(Guid? Id, string? Username, string? Role)
    {
        public static CurrentUser From(ClaimsPrincipal principal) => new(
            Guid.TryParse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub), out var id) ? id : null,
            principal.FindFirstValue(JwtRegisteredClaimNames.UniqueName),
            principal.FindFirstValue("role"));

        public bool IsSignedIn => Id is not null && Roles.IsKnown(Role);

        public bool IsAdmin => IsSignedIn && Role == Roles.Admin;

        /// <summary>
        /// Administrators manage everything. Professors manage what they created. Items without an
        /// owner (the seeded demo data) belong to the administrators.
        /// </summary>
        public bool CanManage(IOwned item) =>
            IsAdmin || (IsSignedIn && Role == Roles.Professor && item.OwnerId is { } owner && owner == Id);

        /// <summary>Marks a new item as created by this account.</summary>
        /// <summary>Published surveys are open to anyone with the link; drafts only to the people who manage them.</summary>
        public bool CanOpen(SurveyDefinition survey) => survey.IsPublished() || CanManage(survey);

        public void Own(IOwned item)
        {
            item.OwnerId = Id;
            item.OwnerName = Username;
        }

        /// <summary>The 403 answer when someone tries to change or read what isn't theirs.</summary>
        public static IResult NotYours(string what) => Results.Problem(
            title: $"You can only manage {what} you created.",
            detail: "Administrators can manage everything; professors and scientists manage their own.",
            statusCode: StatusCodes.Status403Forbidden);
    }
}
