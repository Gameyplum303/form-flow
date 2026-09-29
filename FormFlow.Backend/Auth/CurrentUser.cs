using System.Diagnostics.CodeAnalysis;
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

        /// <summary>Administrators and professors, who build questions and surveys.</summary>
        public bool IsBuilder => IsSignedIn && Roles.CanBuild(Role);

        /// <summary>
        /// Administrators manage everything. Professors manage what they created. Items without an
        /// owner (the seeded demo data) belong to the administrators.
        /// </summary>
        public bool CanManage(IOwned item) =>
            IsAdmin || (IsSignedIn && Role == Roles.Professor && item.OwnerId is { } owner && owner == Id);

        /// <summary>Published surveys are open to anyone with the link; drafts only to the people who manage them.</summary>
        public bool CanOpen(SurveyDefinition survey) => survey.IsPublished() || CanManage(survey);

        /// <summary>
        /// Whether the caller is turned away from managing an item: with 404 when it doesn't exist,
        /// or 403 when it isn't theirs.
        /// </summary>
        public bool CannotManage<T>([NotNullWhen(false)] T? item, string what, [NotNullWhen(true)] out IResult? answer)
            where T : class, IOwned
        {
            answer = item is null ? Results.NotFound() : CanManage(item) ? null : NotYours(what);
            return answer is not null;
        }

        /// <summary>
        /// Hides who created an item from callers who don't build surveys. A professor's username is
        /// their email address, so it stays with the people who manage questions and surveys.
        /// Repositories return a fresh copy on every read, so the stored item keeps its owner.
        /// </summary>
        public T ShowOwnerToBuilders<T>(T item) where T : IOwned
        {
            if (!IsBuilder)
            {
                item.OwnerId = null;
                item.OwnerName = null;
            }
            return item;
        }

        /// <summary>Marks a new item as created by this account.</summary>
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
