using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using FormFlow.Backend.Email;
using FormFlow.Backend.Repositories;
using FormFlow.Data.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FormFlow.Backend.Tests.Endpoints
{
    /// <summary>Email verification, forgotten passwords and changing a password.</summary>
    public class AccountSecurityTests : IDisposable
    {
        private const string Ada = "ada@lab.example";
        private const string AdaPassword = "analytical";

        private readonly InMemoryApiFactory _root = new();
        private readonly ShiftableClock _clock = new();
        private readonly WebApplicationFactory<Program> _factory;
        private readonly HttpClient _client;

        public AccountSecurityTests()
        {
            _factory = _root.WithWebHostBuilder(b => b
                .UseSetting("Accounts:0:Email", "rogers@formflow.example")
                .ConfigureTestServices(services =>
                {
                    services.RemoveAll<TimeProvider>();
                    services.AddSingleton<TimeProvider>(_clock);
                }));
            _client = _factory.CreateClient();
        }

        public void Dispose()
        {
            _factory.Dispose();
            _root.Dispose();
        }

        /// <summary>The system clock, moved forward on demand to expire links.</summary>
        private sealed class ShiftableClock : TimeProvider
        {
            public TimeSpan Offset { get; set; }
            public override DateTimeOffset GetUtcNow() => base.GetUtcNow() + Offset;
        }

        private IReadOnlyList<SentEmail> Outbox => _factory.Services.GetRequiredService<OutboxEmailSender>().Sent;

        private string? Token(string to, string page) => InMemoryApiFactory.EmailedToken(_factory.Services, to, page);

        private Task<HttpResponseMessage> PostAsync<T>(string path, T body) => _client.PostAsJsonAsync(path, body);

        private Task<HttpResponseMessage> LoginAsync(string username, string password) =>
            PostAsync("/api/auth/login", new LoginRequest { Username = username, Password = password });

        /// <summary>A professor who signed up, verified their email and was approved.</summary>
        private async Task SignUpApprovedAsync()
        {
            (await PostAsync("/api/auth/signup", SignUpEndpointTests.ValidSignUp(Ada))).StatusCode.Should().Be(HttpStatusCode.Created);
            await InMemoryApiFactory.VerifyEmailAsync(_client, _factory.Services, Ada);
            var users = _factory.Services.GetRequiredService<IUserRepository>();
            var user = users.FindByUsername(Ada)!;
            user.Status = AccountStatuses.Active;
            users.Update(user);
        }

        private HttpClient SignedIn(string token)
        {
            var client = _factory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return client;
        }

        [Fact]
        public async Task SignUp_EmailsAVerificationLink_ThatWorksOnce()
        {
            await PostAsync("/api/auth/signup", SignUpEndpointTests.ValidSignUp(Ada));

            var email = Outbox.Should().ContainSingle().Subject;
            email.To.Should().Be(Ada);
            email.Subject.Should().Be("Verify your FormFlow email address");
            email.Body.Should().Contain("Hi Ada Lovelace").And.Contain("http://localhost:5224/verify-email?token=");
            var token = Token(Ada, "verify-email")!;

            var verified = await PostAsync("/api/auth/verify-email", new VerifyEmailRequest { Token = token });
            verified.StatusCode.Should().Be(HttpStatusCode.OK);
            (await verified.Content.ReadFromJsonAsync<VerifyEmailResponse>())!.Status.Should().Be(AccountStatuses.Pending,
                "the page says an administrator still has to approve the account");
            _factory.Services.GetRequiredService<IUserRepository>().FindByUsername(Ada)!.EmailVerified.Should().BeTrue();

            var again = await PostAsync("/api/auth/verify-email", new VerifyEmailRequest { Token = token });
            again.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await again.Content.ReadAsStringAsync()).Should().Contain("This link is invalid or has expired.");
        }

        [Fact]
        public async Task VerificationLink_ExpiresAfterTwoDays()
        {
            await PostAsync("/api/auth/signup", SignUpEndpointTests.ValidSignUp(Ada));
            _clock.Offset = AccountEmails.VerifyLinkLifetime + TimeSpan.FromMinutes(1);

            (await PostAsync("/api/auth/verify-email", new VerifyEmailRequest { Token = Token(Ada, "verify-email")! }))
                .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task ResendVerification_SendsANewLink_AndOnlyTheNewestWorks()
        {
            await PostAsync("/api/auth/signup", SignUpEndpointTests.ValidSignUp(Ada));
            var first = Token(Ada, "verify-email")!;

            var resend = await PostAsync("/api/auth/resend-verification", new EmailRequest { Email = "ADA@lab.example" });

            resend.StatusCode.Should().Be(HttpStatusCode.Accepted);
            Outbox.Should().HaveCount(2);
            var second = Token(Ada, "verify-email")!;
            second.Should().NotBe(first);
            (await PostAsync("/api/auth/verify-email", new VerifyEmailRequest { Token = first })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await PostAsync("/api/auth/verify-email", new VerifyEmailRequest { Token = second })).StatusCode.Should().Be(HttpStatusCode.OK);

            // Nothing more to verify, so nothing is sent.
            await PostAsync("/api/auth/resend-verification", new EmailRequest { Email = Ada });
            Outbox.Should().HaveCount(2);
        }

        [Theory]
        [InlineData("/api/auth/forgot-password")]
        [InlineData("/api/auth/resend-verification")]
        public async Task EmailRequests_GiveTheSameAnswer_WhetherOrNotTheAccountExists(string path)
        {
            await PostAsync("/api/auth/signup", SignUpEndpointTests.ValidSignUp(Ada));

            var known = await PostAsync(path, new EmailRequest { Email = Ada });
            var unknown = await PostAsync(path, new EmailRequest { Email = "nobody@lab.example" });

            known.StatusCode.Should().Be(HttpStatusCode.Accepted);
            unknown.StatusCode.Should().Be(HttpStatusCode.Accepted);
            (await unknown.Content.ReadAsStringAsync()).Should().Be(await known.Content.ReadAsStringAsync());
            Outbox.Should().NotContain(e => e.To == "nobody@lab.example");
        }

        [Fact]
        public async Task ForgotPassword_EmailsAOneHourLink_ThatSetsANewPassword()
        {
            await SignUpApprovedAsync();

            await PostAsync("/api/auth/forgot-password", new EmailRequest { Email = Ada });

            var email = Outbox.First();
            email.Subject.Should().Be("Reset your FormFlow password");
            email.Body.Should().Contain("works for one hour");
            var token = Token(Ada, "reset-password")!;

            var tooShort = await PostAsync("/api/auth/reset-password", new ResetPasswordRequest { Token = token, Password = "short" });
            tooShort.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await tooShort.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors").GetProperty("password")[0].GetString()
                .Should().Be("Use at least 8 characters.");

            (await PostAsync("/api/auth/reset-password", new ResetPasswordRequest { Token = token, Password = "difference engine" }))
                .StatusCode.Should().Be(HttpStatusCode.NoContent, "a rejected password doesn't use up the link");

            (await LoginAsync(Ada, AdaPassword)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            (await LoginAsync(Ada, "difference engine")).StatusCode.Should().Be(HttpStatusCode.OK);
            (await PostAsync("/api/auth/reset-password", new ResetPasswordRequest { Token = token, Password = "third password" }))
                .StatusCode.Should().Be(HttpStatusCode.BadRequest, "the link works once");
        }

        [Fact]
        public async Task ResetLink_ExpiresAfterAnHour()
        {
            await SignUpApprovedAsync();
            await PostAsync("/api/auth/forgot-password", new EmailRequest { Email = Ada });
            _clock.Offset = AccountEmails.ResetLinkLifetime + TimeSpan.FromMinutes(1);

            (await PostAsync("/api/auth/reset-password", new ResetPasswordRequest { Token = Token(Ada, "reset-password")!, Password = "difference engine" }))
                .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task VerificationAndResetLinks_CantBeSwapped()
        {
            await PostAsync("/api/auth/signup", SignUpEndpointTests.ValidSignUp(Ada));

            (await PostAsync("/api/auth/reset-password", new ResetPasswordRequest { Token = Token(Ada, "verify-email")!, Password = "difference engine" }))
                .StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await LoginAsync(Ada, "difference engine")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task ResettingThePassword_EndsEarlierSignIns()
        {
            await SignUpApprovedAsync();
            var before = (await (await LoginAsync(Ada, AdaPassword)).Content.ReadFromJsonAsync<LoginResponse>())!.Token;
            (await SignedIn(before).GetAsync("/api/auth/me")).StatusCode.Should().Be(HttpStatusCode.OK);

            // Token times are whole seconds; step past this one so the change is clearly later.
            _clock.Offset = TimeSpan.FromSeconds(2);
            await PostAsync("/api/auth/forgot-password", new EmailRequest { Email = Ada });
            await PostAsync("/api/auth/reset-password", new ResetPasswordRequest { Token = Token(Ada, "reset-password")!, Password = "difference engine" });

            (await SignedIn(before).GetAsync("/api/auth/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task ResettingThePassword_AlsoVerifiesTheEmailAddress()
        {
            await PostAsync("/api/auth/signup", SignUpEndpointTests.ValidSignUp(Ada));
            await PostAsync("/api/auth/forgot-password", new EmailRequest { Email = Ada });

            await PostAsync("/api/auth/reset-password", new ResetPasswordRequest { Token = Token(Ada, "reset-password")!, Password = "difference engine" });

            var login = await LoginAsync(Ada, "difference engine");
            (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("reason").GetString().Should().Be(SignInBlocks.Pending);
        }

        [Fact]
        public async Task ConfiguredAccounts_WithAnEmail_CanResetTheirPassword()
        {
            await PostAsync("/api/auth/forgot-password", new EmailRequest { Email = "Rogers@FormFlow.example" });

            var token = Token("rogers@formflow.example", "reset-password");
            token.Should().NotBeNull();
            Outbox.Single().Body.Should().Contain("Hi Rogers");
            await PostAsync("/api/auth/reset-password", new ResetPasswordRequest { Token = token!, Password = "a new password" });
            (await LoginAsync("Rogers", "a new password")).StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task ChangePassword_ChecksTheCurrentOne_AndSignsOutOtherSessions()
        {
            var other = (await (await LoginAsync("professor", "password")).Content.ReadFromJsonAsync<LoginResponse>())!.Token;
            var client = SignedIn(other);
            _clock.Offset = TimeSpan.FromSeconds(2);

            var wrong = await client.PostAsJsonAsync("/api/auth/change-password",
                new ChangePasswordRequest { CurrentPassword = "nope", NewPassword = "a new password" });
            wrong.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await wrong.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors").GetProperty("currentPassword")[0].GetString()
                .Should().Be("That isn't your current password.");

            var tooShort = await client.PostAsJsonAsync("/api/auth/change-password",
                new ChangePasswordRequest { CurrentPassword = "password", NewPassword = "short" });
            (await tooShort.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors").GetProperty("newPassword")[0].GetString()
                .Should().Be("Use at least 8 characters.");

            var changed = await client.PostAsJsonAsync("/api/auth/change-password",
                new ChangePasswordRequest { CurrentPassword = "password", NewPassword = "a new password" });
            changed.StatusCode.Should().Be(HttpStatusCode.OK);
            var fresh = (await changed.Content.ReadFromJsonAsync<LoginResponse>())!.Token;

            (await SignedIn(other).GetAsync("/api/auth/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            (await SignedIn(fresh).GetAsync("/api/auth/me")).StatusCode.Should().Be(HttpStatusCode.OK, "the session that changed it carries on");
            (await LoginAsync("professor", "a new password")).StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task DeletedAccounts_LoseTheirSignIn()
        {
            await SignUpApprovedAsync();
            var token = (await (await LoginAsync(Ada, AdaPassword)).Content.ReadFromJsonAsync<LoginResponse>())!.Token;
            var users = _factory.Services.GetRequiredService<IUserRepository>();

            users.Delete(users.FindByUsername(Ada)!.Id);

            (await SignedIn(token).GetAsync("/api/auth/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task Outbox_ListsSentEmails_ForAdministrators()
        {
            await PostAsync("/api/auth/signup", SignUpEndpointTests.ValidSignUp(Ada));

            var outbox = await _factory.CreateClient().AsAdmin().GetFromJsonAsync<List<SentEmail>>("/api/accounts/outbox");

            outbox!.Should().ContainSingle(e => e.To == Ada && e.Subject == "Verify your FormFlow email address");
        }

        [Fact]
        public async Task WithAnSmtpServer_ThereIsNoOutbox()
        {
            using var smtp = _root.WithWebHostBuilder(b => b.UseSetting("Email:Smtp:Host", "smtp.example.com"));

            smtp.Services.GetRequiredService<IEmailSender>().Should().BeOfType<SmtpEmailSender>();
            (await smtp.CreateClient().AsAdmin().GetAsync("/api/accounts/outbox")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task FailedEmails_DontFailTheSignUp()
        {
            using var broken = _root.WithWebHostBuilder(b => b.ConfigureTestServices(services =>
            {
                services.RemoveAll<IEmailSender>();
                services.AddSingleton<IEmailSender, FailingSender>();
            }));

            var response = await broken.CreateClient().PostAsJsonAsync("/api/auth/signup", SignUpEndpointTests.ValidSignUp(Ada));

            response.StatusCode.Should().Be(HttpStatusCode.Created, "the person can ask for the email again");
        }

        [Fact]
        public async Task EmailedLinkRequests_AreRateLimited_SeparatelyFromSignIn()
        {
            using var limited = _root.WithWebHostBuilder(b => b.UseSetting("RateLimits:AccountPerMinute", "2"));
            var client = limited.CreateClient();

            for (var i = 0; i < 2; i++)
            {
                (await client.PostAsJsonAsync("/api/auth/forgot-password", new EmailRequest { Email = Ada }))
                    .StatusCode.Should().Be(HttpStatusCode.Accepted);
            }

            (await client.PostAsJsonAsync("/api/auth/forgot-password", new EmailRequest { Email = Ada }))
                .StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
            (await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Username = "professor", Password = "password" }))
                .StatusCode.Should().Be(HttpStatusCode.OK);
        }

        private sealed class FailingSender : IEmailSender
        {
            public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default) =>
                throw new System.Net.Mail.SmtpException("The mail server is down.");
        }
    }
}
