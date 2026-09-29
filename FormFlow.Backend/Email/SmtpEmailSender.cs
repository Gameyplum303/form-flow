using System.Net;
using System.Net.Mail;

namespace FormFlow.Backend.Email
{
    /// <summary>Sends emails through the SMTP server in Email:Smtp.</summary>
    public class SmtpEmailSender(EmailSettings settings) : IEmailSender
    {
        public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
        {
            using var client = new SmtpClient(settings.Smtp.Host, settings.Smtp.Port)
            {
                EnableSsl = settings.Smtp.EnableSsl,
                DeliveryMethod = SmtpDeliveryMethod.Network,
            };
            if (!string.IsNullOrEmpty(settings.Smtp.Username))
            {
                client.Credentials = new NetworkCredential(settings.Smtp.Username, settings.Smtp.Password);
            }

            using var mail = new MailMessage
            {
                From = new MailAddress(settings.From),
                Subject = message.Subject,
                Body = message.Body,
                IsBodyHtml = false,
            };
            mail.To.Add(message.To);
            await client.SendMailAsync(mail, cancellationToken);
        }
    }
}
