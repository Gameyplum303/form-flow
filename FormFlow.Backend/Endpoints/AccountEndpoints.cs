using FormFlow.Backend.Auth;
using FormFlow.Backend.Email;
using FormFlow.Backend.Repositories;
using FormFlow.Data.Models;

namespace FormFlow.Backend.Endpoints
{
    /// <summary>Administrators review professor/scientist sign-ups here.</summary>
    public static class AccountEndpoints
    {
        public static void MapAccountEndpoints(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("/api/accounts").WithTags("Accounts").RequireAuthorization(JwtSettings.AdminPolicy);

            group.MapGet("/pending", (IUserRepository users) => Results.Ok(users.FindPending().Select(ToPending)))
                .WithName("GetPendingAccounts")
                .Produces<List<PendingAccount>>(StatusCodes.Status200OK)
                .Produces(StatusCodes.Status401Unauthorized)
                .Produces(StatusCodes.Status403Forbidden);

            group.MapPost("/{id:guid}/approve", (Guid id, IUserRepository users) =>
            {
                if (users.FindById(id) is not { Status: AccountStatuses.Pending } user)
                {
                    return NoPendingSignUp();
                }

                user.Status = AccountStatuses.Active;
                users.Update(user);
                return Results.NoContent();
            })
            .WithName("ApproveAccount")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

            // Declining removes the sign-up, so the person can sign up again later.
            group.MapPost("/{id:guid}/decline", (Guid id, IUserRepository users, IAccountTokenRepository tokens) =>
            {
                if (users.FindById(id) is not { Status: AccountStatuses.Pending })
                {
                    return NoPendingSignUp();
                }

                users.Delete(id);
                tokens.DeleteForUser(id);
                return Results.NoContent();
            })
            .WithName("DeclineAccount")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

            // Without an SMTP server, emails wait here so an administrator (or a test) can open their links.
            group.MapGet("/outbox", (IEmailSender sender) => sender is OutboxEmailSender outbox
                    ? Results.Ok(outbox.Sent)
                    : Results.Problem(title: "Emails are sent through SMTP, so there is no outbox.", statusCode: StatusCodes.Status404NotFound))
                .WithName("GetOutbox")
                .Produces<List<SentEmail>>(StatusCodes.Status200OK)
                .ProducesProblem(StatusCodes.Status404NotFound);
        }

        private static IResult NoPendingSignUp() =>
            Results.Problem(title: "There is no sign-up waiting with that id.", statusCode: StatusCodes.Status404NotFound);

        private static PendingAccount ToPending(AdminUser user) => new()
        {
            Id = user.Id,
            Name = user.Name ?? user.Username,
            Email = user.Email ?? string.Empty,
            DateOfBirth = user.DateOfBirth ?? string.Empty,
            IntendedUse = user.IntendedUse ?? string.Empty,
            Organization = user.Organization ?? string.Empty,
            CreatedAt = user.CreatedAt,
            EmailVerified = user.EmailVerified,
        };
    }
}
