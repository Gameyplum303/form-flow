namespace FormFlow.Backend.Email
{
    /// <summary>A plain-text email.</summary>
    public record EmailMessage(string To, string Subject, string Body);

    public interface IEmailSender
    {
        Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
    }
}
