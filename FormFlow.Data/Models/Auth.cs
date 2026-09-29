namespace FormFlow.Data.Models
{
    /// <summary>Body of POST /api/auth/login.</summary>
    public class LoginRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    /// <summary>A signed-in account's bearer token.</summary>
    public class LoginResponse
    {
        public string Token { get; set; } = string.Empty;

        /// <summary>The account's id, which surveys and questions record as their owner.</summary>
        public Guid UserId { get; set; }
        public string Username { get; set; } = string.Empty;

        /// <summary>One of <see cref="Roles"/>.</summary>
        public string Role { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
    }

    /// <summary>Whether an account can sign in yet.</summary>
    public static class AccountStatuses
    {
        public const string Active = "active";

        /// <summary>Signed up and waiting for an administrator to approve the account.</summary>
        public const string Pending = "pending";
    }

    /// <summary>Body of POST /api/auth/signup, which asks for a professor/scientist account.</summary>
    public class SignUpRequest
    {
        public string Name { get; set; } = string.Empty;

        /// <summary>Also the name the account signs in with.</summary>
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;

        /// <summary>An ISO date (YYYY-MM-DD).</summary>
        public string DateOfBirth { get; set; } = string.Empty;

        /// <summary>What the person plans to use FormFlow for.</summary>
        public string IntendedUse { get; set; } = string.Empty;

        /// <summary>The university, lab or company they work for.</summary>
        public string Organization { get; set; } = string.Empty;
    }

    /// <summary>The result of a sign-up: <see cref="AccountStatuses.Pending"/> until an administrator approves it.</summary>
    public class SignUpResponse
    {
        public string Status { get; set; } = string.Empty;

        /// <summary>True when the account can't sign in until its email address is verified with the emailed link.</summary>
        public bool EmailVerificationRequired { get; set; }
    }

    /// <summary>
    /// Why a correct password still can't sign in, sent as the "reason" of the 403 problem so clients
    /// can offer the right next step.
    /// </summary>
    public static class SignInBlocks
    {
        public const string EmailUnverified = "email_unverified";
        public const string Pending = "pending";
    }

    /// <summary>Body of the requests that only need an email address: forgot password and resend verification.</summary>
    public class EmailRequest
    {
        public string Email { get; set; } = string.Empty;
    }

    /// <summary>Body of POST /api/auth/verify-email: the token from the emailed link.</summary>
    public class VerifyEmailRequest
    {
        public string Token { get; set; } = string.Empty;
    }

    /// <summary>The verified account's status, so the page can say whether it still waits for approval.</summary>
    public class VerifyEmailResponse
    {
        public string Status { get; set; } = string.Empty;
    }

    /// <summary>Body of POST /api/auth/reset-password: the token from the emailed link and the new password.</summary>
    public class ResetPasswordRequest
    {
        public string Token { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    /// <summary>Body of POST /api/auth/change-password, for a signed-in account.</summary>
    public class ChangePasswordRequest
    {
        public string CurrentPassword { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
    }

    /// <summary>An email the API sent, as the outbox lists it when no mail server is configured.</summary>
    public class SentEmail
    {
        public string To { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public DateTime SentAt { get; set; }
    }

    /// <summary>A professor/scientist sign-up waiting for an administrator, as the review page lists it.</summary>
    public class PendingAccount
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string DateOfBirth { get; set; } = string.Empty;
        public string IntendedUse { get; set; } = string.Empty;
        public string Organization { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }

        /// <summary>Whether they have opened the verification link sent to their email address.</summary>
        public bool EmailVerified { get; set; }
    }
}
