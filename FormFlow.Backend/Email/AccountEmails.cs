using FormFlow.Backend.Auth;
using FormFlow.Backend.Repositories;

namespace FormFlow.Backend.Email
{
    /// <summary>
    /// Writes and sends the emails about an account: verifying its email address and resetting its
    /// password. A failed send is logged rather than thrown, so the request that caused it still
    /// succeeds and the person can ask for the email again.
    /// </summary>
    public class AccountEmails(IEmailSender sender, IAccountTokenRepository tokens, EmailSettings settings,
        TimeProvider clock, ILogger<AccountEmails> logger)
    {
        public static readonly TimeSpan VerifyLinkLifetime = TimeSpan.FromDays(2);
        public static readonly TimeSpan ResetLinkLifetime = TimeSpan.FromHours(1);

        public Task SendVerificationAsync(AdminUser user)
        {
            var token = tokens.Create(user.Id, AccountTokenPurposes.VerifyEmail, clock.GetUtcNow().UtcDateTime.Add(VerifyLinkLifetime));
            return SendAsync(user, "Verify your FormFlow email address", $"""
                Hi {Greeting(user)},

                Thanks for signing up for FormFlow. Open this link to verify your email address:

                {settings.Link("verify-email", token)}

                The link works for {Describe(VerifyLinkLifetime)}. If you didn't sign up, you can ignore this email.
                """);
        }

        public Task SendPasswordResetAsync(AdminUser user)
        {
            var token = tokens.Create(user.Id, AccountTokenPurposes.ResetPassword, clock.GetUtcNow().UtcDateTime.Add(ResetLinkLifetime));
            return SendAsync(user, "Reset your FormFlow password", $"""
                Hi {Greeting(user)},

                Someone asked to reset the password for your FormFlow account. Open this link to choose a new one:

                {settings.Link("reset-password", token)}

                The link works for {Describe(ResetLinkLifetime)} and only once. If you didn't ask for this, you can ignore this email;
                your password stays the same.
                """);
        }

        private async Task SendAsync(AdminUser user, string subject, string body)
        {
            try
            {
                await sender.SendAsync(new EmailMessage(user.Email!, subject, body));
            }
            catch (Exception e) when (e is System.Net.Mail.SmtpException or InvalidOperationException or IOException)
            {
                logger.LogError(e, "Could not send \"{Subject}\" to account {UserId}.", subject, user.Id);
            }
        }

        /// <summary>A link lifetime in words, such as "2 days" or "one hour".</summary>
        private static string Describe(TimeSpan lifetime)
        {
            var (count, unit) = lifetime.TotalDays >= 1 ? ((int)lifetime.TotalDays, "day") : ((int)lifetime.TotalHours, "hour");
            return count == 1 ? $"one {unit}" : $"{count} {unit}s";
        }

        private static string Greeting(AdminUser user) => string.IsNullOrWhiteSpace(user.Name) ? user.Username : user.Name;
    }
}
