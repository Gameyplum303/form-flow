namespace FormFlow.Backend.Email
{
    /// <summary>
    /// The "Email" configuration section. With Email:Smtp:Host set, emails go out through that SMTP
    /// server (any provider works, such as SendGrid, Mailgun, Amazon SES or Gmail). Without it they
    /// stay in an in-memory outbox that administrators can read, which is enough for development,
    /// tests and demos.
    /// </summary>
    public class EmailSettings
    {
        public const string Section = "Email";

        /// <summary>The address emails come from.</summary>
        public string From { get; set; } = "FormFlow <no-reply@formflow.local>";

        /// <summary>The Blazor app's address, used to build the links in emails.</summary>
        public string LinkBaseUrl { get; set; } = "http://localhost:5224/";

        public SmtpSettings Smtp { get; set; } = new();

        public bool UsesSmtp => !string.IsNullOrWhiteSpace(Smtp.Host);

        /// <summary>A link to a page of the Blazor app, for example Link("reset-password", token).</summary>
        public string Link(string page, string token) =>
            $"{LinkBaseUrl.TrimEnd('/')}/{page}?token={Uri.EscapeDataString(token)}";
    }

    public class SmtpSettings
    {
        public string? Host { get; set; }
        public int Port { get; set; } = 587;
        public string? Username { get; set; }
        public string? Password { get; set; }

        /// <summary>Use TLS. On by default; turn it off only for a local test server.</summary>
        public bool EnableSsl { get; set; } = true;
    }
}
