using System.Security.Claims;
using FormFlow.Backend.Auth;
using FormFlow.Backend.Email;
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

        /// <summary>Sign-up and the emailed-link requests, limited separately from sign-in.</summary>
        public const string AccountRateLimit = "account";

        /// <summary>When true (the default), new sign-ups wait for an administrator's approval.</summary>
        public const string RequireApprovalSetting = "SignUp:RequireApproval";

        /// <summary>When true (the default), new sign-ups can't sign in until they open the link emailed to them.</summary>
        public const string RequireEmailVerificationSetting = "SignUp:RequireEmailVerification";

        public const string PendingMessage = "Your account is waiting for an administrator's approval.";
        public const string VerifyEmailMessage = "Please verify your email address first.";
        public const string InvalidLinkMessage = "This link is invalid or has expired. Ask for a new one.";

        /// <summary>
        /// The answer to "forgot password" and "resend verification", whether or not an account uses the
        /// address, so the API doesn't reveal who has an account.
        /// </summary>
        public const string EmailSentMessage = "If an account uses that email address, we've sent it a link.";

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

                if (!user.EmailVerified)
                {
                    return Results.Problem(title: VerifyEmailMessage,
                        detail: "Open the link we emailed you, or ask for a new one.",
                        statusCode: StatusCodes.Status403Forbidden,
                        extensions: new Dictionary<string, object?> { ["reason"] = SignInBlocks.EmailUnverified });
                }

                if (user.Status == AccountStatuses.Pending)
                {
                    return Results.Problem(title: PendingMessage,
                        detail: "You'll be able to sign in once an administrator approves your account.",
                        statusCode: StatusCodes.Status403Forbidden,
                        extensions: new Dictionary<string, object?> { ["reason"] = SignInBlocks.Pending });
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
            group.MapPost("/signup", async (SignUpRequest request, IUserRepository users, IPasswordHasher<AdminUser> hasher,
                IConfiguration config, TimeProvider clock, AccountEmails emails) =>
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
                var requireVerification = config.GetValue(RequireEmailVerificationSetting, true);
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
                    EmailVerified = !requireVerification,
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

                if (requireVerification)
                {
                    await emails.SendVerificationAsync(user);
                }

                return Results.Json(new SignUpResponse { Status = user.Status, EmailVerificationRequired = requireVerification },
                    statusCode: StatusCodes.Status201Created);
            })
            .WithName("SignUp")
            .RequireRateLimiting(AccountRateLimit)
            .Produces<SignUpResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .ProducesValidationProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

            group.MapPost("/verify-email", (VerifyEmailRequest request, IUserRepository users, IAccountTokenRepository tokens,
                TimeProvider clock) =>
            {
                var userId = tokens.Redeem(request.Token ?? string.Empty, AccountTokenPurposes.VerifyEmail, clock.GetUtcNow().UtcDateTime);
                if (userId is not { } id || users.FindById(id) is not { } user)
                {
                    return InvalidLink();
                }

                user.EmailVerified = true;
                users.Update(user);
                return Results.Ok(new VerifyEmailResponse { Status = user.Status });
            })
            .WithName("VerifyEmail")
            .RequireRateLimiting(AccountRateLimit)
            .Produces<VerifyEmailResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

            group.MapPost("/resend-verification", async (EmailRequest request, IUserRepository users, AccountEmails emails) =>
            {
                if (users.FindByEmail(request.Email ?? string.Empty) is { EmailVerified: false } user)
                {
                    await emails.SendVerificationAsync(user);
                }
                return EmailSent();
            })
            .WithName("ResendVerification")
            .RequireRateLimiting(AccountRateLimit)
            .Produces(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

            group.MapPost("/forgot-password", async (EmailRequest request, IUserRepository users, AccountEmails emails) =>
            {
                if (users.FindByEmail(request.Email ?? string.Empty) is { } user)
                {
                    await emails.SendPasswordResetAsync(user);
                }
                return EmailSent();
            })
            .WithName("ForgotPassword")
            .RequireRateLimiting(AccountRateLimit)
            .Produces(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

            group.MapPost("/reset-password", (ResetPasswordRequest request, IUserRepository users, IAccountTokenRepository tokens,
                IPasswordHasher<AdminUser> hasher, TimeProvider clock) =>
            {
                // Check the password before using up the link, so a too-short password can be fixed and resent.
                if (SignUpValidator.PasswordError(request.Password) is { } passwordError)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]> { ["password"] = [passwordError] });
                }

                var now = clock.GetUtcNow().UtcDateTime;
                var userId = tokens.Redeem(request.Token ?? string.Empty, AccountTokenPurposes.ResetPassword, now);
                if (userId is not { } id || users.FindById(id) is not { } user)
                {
                    return InvalidLink();
                }

                SetPassword(user, request.Password, hasher, now);
                // The link arrived by email, which proves the address works.
                user.EmailVerified = true;
                users.Update(user);
                return Results.NoContent();
            })
            .WithName("ResetPassword")
            .RequireRateLimiting(AccountRateLimit)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

            group.MapPost("/change-password", (ChangePasswordRequest request, ClaimsPrincipal principal, IUserRepository users,
                IPasswordHasher<AdminUser> hasher, TokenService tokens, TimeProvider clock) =>
            {
                if (CurrentUser.From(principal).Id is not { } id || users.FindById(id) is not { } user)
                {
                    return Results.Unauthorized();
                }

                if (hasher.VerifyHashedPassword(user, user.PasswordHash, request.CurrentPassword ?? string.Empty)
                    == PasswordVerificationResult.Failed)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["currentPassword"] = ["That isn't your current password."],
                    });
                }
                if (SignUpValidator.PasswordError(request.NewPassword) is { } passwordError)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]> { ["newPassword"] = [passwordError] });
                }

                SetPassword(user, request.NewPassword, hasher, clock.GetUtcNow().UtcDateTime);
                users.Update(user);

                // Other sign-ins stop working; this one gets a fresh token so it carries on.
                return Results.Ok(tokens.CreateToken(user));
            })
            .WithName("ChangePassword")
            .RequireAuthorization(JwtSettings.SignedInPolicy)
            .RequireRateLimiting(AccountRateLimit)
            .Produces<LoginResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized);

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

        private static IResult InvalidLink() =>
            Results.Problem(title: InvalidLinkMessage, statusCode: StatusCodes.Status400BadRequest);

        private static IResult EmailSent() => Results.Json(new { message = EmailSentMessage }, statusCode: StatusCodes.Status202Accepted);

        private static void SetPassword(AdminUser user, string password, IPasswordHasher<AdminUser> hasher, DateTime now)
        {
            user.PasswordHash = hasher.HashPassword(user, password);
            user.PasswordChangedAt = now;
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
