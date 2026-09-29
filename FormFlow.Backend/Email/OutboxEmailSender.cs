using System.Collections.Concurrent;
using FormFlow.Data.Models;

namespace FormFlow.Backend.Email
{
    /// <summary>
    /// Keeps the latest emails in memory instead of sending them, for when no SMTP server is configured.
    /// Administrators can read them through GET /api/accounts/outbox, and each one is also logged.
    /// </summary>
    public class OutboxEmailSender(TimeProvider clock, ILogger<OutboxEmailSender> logger) : IEmailSender
    {
        public const int Capacity = 100;

        private readonly ConcurrentQueue<SentEmail> _sent = new();

        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
        {
            _sent.Enqueue(new SentEmail
            {
                To = message.To,
                Subject = message.Subject,
                Body = message.Body,
                SentAt = clock.GetUtcNow().UtcDateTime,
            });
            while (_sent.Count > Capacity)
            {
                _sent.TryDequeue(out _);
            }

            logger.LogInformation("No SMTP server is configured, so this email is in the outbox. To {To}: {Subject}",
                message.To, message.Subject);
            return Task.CompletedTask;
        }

        /// <summary>The emails in the outbox, newest first.</summary>
        public IReadOnlyList<SentEmail> Sent => _sent.Reverse().ToList();
    }
}
