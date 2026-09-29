using System.Security.Claims;
using FormFlow.Backend.Auth;
using FormFlow.Backend.Repositories;
using FormFlow.Data.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.JsonWebTokens;

namespace FormFlow.Backend.Endpoints
{
    public static class AuthEndpoints
    {
        public const string LoginRateLimit = "login";

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

                return Results.Ok(tokens.CreateToken(user));
            })
            .WithName("Login")
            .RequireRateLimiting(LoginRateLimit)
            .Produces<LoginResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

            group.MapGet("/me", (ClaimsPrincipal user) =>
                Results.Ok(new { username = user.FindFirstValue(JwtRegisteredClaimNames.UniqueName) }))
            .WithName("CurrentAdmin")
            .RequireAuthorization(JwtSettings.AdminPolicy)
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
