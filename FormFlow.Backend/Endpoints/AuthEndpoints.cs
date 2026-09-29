using System.Security.Claims;
using FormFlow.Backend.Auth;
using FormFlow.Backend.Repositories;
using FormFlow.Data.Models;
using FormFlow.Data.Services;
using LiteDB;
using Microsoft.AspNetCore.Identity;

namespace FormFlow.Backend.Endpoints
{
    public static class AuthEndpoints
    {
        public const string LoginRateLimit = "login";

        /// <summary>When true (the default), new sign-ups wait for an administrator's approval.</summary>
        public const string RequireApprovalSetting = "SignUp:RequireApproval";

        public const string PendingMessage = "Your account is waiting for an administrator's approval.";

        private static IResult EmailTaken() => Results.ValidationProblem(
            new Dictionary<string, string[]> { ["email"] = ["An account with this email already exists. Try signing in."] },
            statusCode: StatusCodes.Status409Conflict);

        public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("/api/auth").WithTags("Auth");

            group.MapPost("/login", (LoginRequest request, IUserRepository users, IPasswordHasher<AdminUser> hasher,
                TokenService tokens) =>
            {
                var user = users.FindByUsername(request.Username ?? string.Empty);

                // Hash even when the user doesn't exist, so response times don't reveal which usernames are real.
                var result = hasher.VerifyHashedPassword(user ?? DummyUser, user?.PasswordHash ?? DummyUser.PasswordHash,
                    request.Password ?? string.Empty);

                if (user is null || result == PasswordVerificationResult.Failed)
                {
                    return Results.Problem(title: "Invalid username or password.", statusCode: StatusCodes.Status401Unauthorized);
                }

                if (user.Status == AccountStatuses.Pending)
                {
                    return Results.Problem(title: PendingMessage,
                        detail: "You'll be able to sign in once an administrator approves your account.",
                        statusCode: StatusCodes.Status403Forbidden);
                }

                return Results.Ok(tokens.CreateToken(user));
            })
            .WithName("Login")
            .RequireRateLimiting(LoginRateLimit)
            .Produces<LoginResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

            // Professors and scientists ask for an account here. Administrators are added through
            // configuration, and students take surveys without an account.
            group.MapPost("/signup", (SignUpRequest request, IUserRepository users, IPasswordHasher<AdminUser> hasher,
                IConfiguration config, TimeProvider clock) =>
            {
                var errors = SignUpValidator.Validate(request, DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime));
                if (errors.Count > 0)
                {
                    return Results.ValidationProblem(errors);
                }

                var email = request.Email.Trim();
                if (users.FindByUsername(email) is not null)
                {
                    return EmailTaken();
                }

                var requireApproval = config.GetValue(RequireApprovalSetting, true);
                var user = new AdminUser
                {
                    Id = Guid.NewGuid(),
                    Username = email,
                    Email = email,
                    Name = request.Name.Trim(),
                    DateOfBirth = request.DateOfBirth.Trim(),
                    IntendedUse = request.IntendedUse.Trim(),
                    Organization = request.Organization.Trim(),
                    Role = Roles.Professor,
                    Status = requireApproval ? AccountStatuses.Pending : AccountStatuses.Active,
                    CreatedAt = clock.GetUtcNow().UtcDateTime,
                };
                user.PasswordHash = hasher.HashPassword(user, request.Password);

                try
                {
                    users.Insert(user);
                }
                catch (LiteException e) when (e.ErrorCode == LiteException.INDEX_DUPLICATE_KEY)
                {
                    // Someone signed up with the same email at the same moment.
                    return EmailTaken();
                }

                return Results.Json(new SignUpResponse { Status = user.Status }, statusCode: StatusCodes.Status201Created);
            })
            .WithName("SignUp")
            .RequireRateLimiting(LoginRateLimit)
            .Produces<SignUpResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .ProducesValidationProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

            group.MapGet("/me", (ClaimsPrincipal principal) =>
            {
                var user = CurrentUser.From(principal);
                return Results.Ok(new { id = user.Id, username = user.Username, role = user.Role });
            })
            .WithName("CurrentUser")
            .RequireAuthorization(JwtSettings.SignedInPolicy)
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized);
        }

        private static readonly AdminUser DummyUser = CreateDummyUser();

        private static AdminUser CreateDummyUser()
        {
            var user = new AdminUser { Username = "nobody" };
            user.PasswordHash = new PasswordHasher<AdminUser>().HashPassword(user, Guid.NewGuid().ToString());
            return user;
        }
    }
}
