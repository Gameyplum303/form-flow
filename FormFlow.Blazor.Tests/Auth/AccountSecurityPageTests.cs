using Bunit;
using FluentAssertions;
using FormFlow.Blazor.Components.Pages;
using FormFlow.Blazor.Components.Pages.Admin;
using FormFlow.Blazor.Services;
using FormFlow.Data.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;

namespace FormFlow.Blazor.Tests.Auth;

/// <summary>Forgot password, reset password, email verification, and changing a password.</summary>
public class AccountSecurityPageTests
{
    private readonly FakeAuthService _auth = new();
    private readonly FakeSessionStorage _storage = new();
    private readonly OutboxAccounts _accounts = new();

    private BunitContext CreateContext(AdminSession? session = null)
    {
        var ctx = new BunitContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        ctx.Services.AddMudServices();
        ctx.Services.AddSingleton<IAuthService>(_auth);
        ctx.Services.AddSingleton<IAccountService>(_accounts);
        ctx.Services.AddSingleton(session ?? _storage.CreateSession());
        ctx.Services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        return ctx;
    }

    [Fact]
    public async Task Login_links_to_forgot_password()
    {
        await using var ctx = CreateContext();

        var cut = ctx.Render<Login>();

        cut.Find("a[href='/forgot-password']").TextContent.Should().Be("Forgot your password?");
    }

    [Fact]
    public async Task Login_offers_a_new_verification_link_when_the_email_is_unverified()
    {
        await using var ctx = CreateContext();
        _auth.Result = (null, AuthService.VerifyEmailMessage);
        var cut = ctx.Render<Login>();
        cut.Find("input[autocomplete=username]").Change("ada@lab.example ");
        cut.Find("input[type=password]").Change("analytical");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => cut.Markup.Should().Contain(AuthService.VerifyEmailMessage));
        cut.FindAll("button").Single(b => b.TextContent.Trim() == "Email me a new link").Click();

        cut.WaitForAssertion(() => cut.Find("[data-verification-sent]").TextContent.Should().Contain("We sent a new verification link to ada@lab.example."));
        _auth.VerificationRequests.Should().Equal("ada@lab.example");
    }

    [Fact]
    public async Task Login_hides_the_resend_button_for_other_errors()
    {
        await using var ctx = CreateContext();
        _auth.Result = (null, "Invalid username or password.");
        var cut = ctx.Render<Login>();
        cut.Find("input[autocomplete=username]").Change("ada@lab.example");
        cut.Find("input[type=password]").Change("wrong");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Invalid username or password."));
        cut.FindAll("button").Should().NotContain(b => b.TextContent.Contains("new link"));
    }

    [Fact]
    public async Task ForgotPassword_sends_the_link_and_says_so_either_way()
    {
        await using var ctx = CreateContext();
        var cut = ctx.Render<ForgotPassword>();

        cut.Find("input[autocomplete=email]").Change(" ada@lab.example ");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => cut.Find("[data-reset-sent]").TextContent.Should()
            .Contain("If an account uses ada@lab.example, we've emailed it a link").And.Contain("one hour"));
        _auth.ResetRequests.Should().Equal("ada@lab.example");
    }

    [Fact]
    public async Task ForgotPassword_checks_the_address_first()
    {
        await using var ctx = CreateContext();
        var cut = ctx.Render<ForgotPassword>();

        cut.Find("input[autocomplete=email]").Change("not an email");
        cut.Find("form").Submit();

        cut.Markup.Should().Contain("Enter an email address, like name@example.com.");
        _auth.ResetRequests.Should().BeEmpty();
    }

    private IRenderedComponent<ResetPassword> RenderReset(BunitContext ctx, string? token = "link-token")
    {
        var nav = ctx.Services.GetRequiredService<NavigationManager>();
        nav.NavigateTo(token is null ? "/reset-password" : $"/reset-password?token={token}");
        return ctx.Render<ResetPassword>();
    }

    [Fact]
    public async Task ResetPassword_saves_the_new_password_with_the_token_from_the_link()
    {
        await using var ctx = CreateContext();
        var cut = RenderReset(ctx);

        cut.FindAll("input[type=password]")[0].Change("difference engine");
        cut.FindAll("input[type=password]")[1].Change("difference engine");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => cut.Find("[data-reset-done]").TextContent.Should().Contain("Your password was changed."));
        _auth.LastReset.Should().Be(("link-token", "difference engine"));
    }

    [Fact]
    public async Task ResetPassword_checks_the_rules_and_the_confirmation_before_sending()
    {
        await using var ctx = CreateContext();
        var cut = RenderReset(ctx);

        cut.FindAll("input[type=password]")[0].Change("short");
        cut.Find("form").Submit();
        cut.Find("[data-field-error=password]").TextContent.Trim().Should().Be("Use at least 8 characters.");

        cut.FindAll("input[type=password]")[0].Change("difference engine");
        cut.FindAll("input[type=password]")[1].Change("different engine");
        cut.Find("form").Submit();
        cut.Find("[data-field-error=confirmPassword]").TextContent.Trim().Should().Be("The passwords don't match.");
        _auth.LastReset.Should().BeNull();
    }

    [Fact]
    public async Task ResetPassword_explains_an_expired_link_and_offers_a_new_one()
    {
        await using var ctx = CreateContext();
        _auth.ResetResult = FormResult.Failed(AuthService.InvalidLinkMessage);
        var cut = RenderReset(ctx);

        cut.FindAll("input[type=password]")[0].Change("difference engine");
        cut.FindAll("input[type=password]")[1].Change("difference engine");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => cut.Markup.Should().Contain(AuthService.InvalidLinkMessage));
        cut.Find("a[href='/forgot-password']").TextContent.Should().Be("Get a new link");
    }

    [Fact]
    public async Task ResetPassword_without_a_token_points_to_forgot_password()
    {
        await using var ctx = CreateContext();
        var cut = RenderReset(ctx, token: null);

        cut.Markup.Should().Contain("This page needs the link from your password reset email.");
        cut.FindAll("input[type=password]").Should().BeEmpty();
    }

    [Theory]
    [InlineData(AccountStatuses.Pending, "An administrator will review your sign-up")]
    [InlineData(AccountStatuses.Active, "You can sign in now.")]
    public async Task VerifyEmail_uses_the_link_once_the_page_is_interactive(string status, string message)
    {
        await using var ctx = CreateContext();
        _auth.VerifyResult = (status, null);
        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo("/verify-email?token=abc");

        var cut = ctx.Render<VerifyEmail>();

        cut.WaitForAssertion(() => cut.Find("[data-email-verified]").TextContent.Should().Contain(message));
        _auth.VerifiedTokens.Should().Equal("abc");
    }

    [Fact]
    public async Task VerifyEmail_explains_a_used_link()
    {
        await using var ctx = CreateContext();
        _auth.VerifyResult = (null, AuthService.InvalidLinkMessage);
        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo("/verify-email?token=used");

        var cut = ctx.Render<VerifyEmail>();

        cut.WaitForAssertion(() => cut.Markup.Should().Contain(AuthService.InvalidLinkMessage));
    }

    private async Task<AdminSession> SignedInSessionAsync()
    {
        var session = _storage.CreateSession();
        await session.SignInAsync(FakeSessionStorage.Login("professor", role: Roles.Professor));
        return session;
    }

    private static void FillChange(IRenderedComponent<AccountSettings> cut, string current, string password, string? confirm = null)
    {
        var inputs = cut.FindAll("input[type=password]");
        inputs[0].Change(current);
        inputs[1].Change(password);
        inputs[2].Change(confirm ?? password);
        cut.Find("form").Submit();
    }

    [Fact]
    public async Task ChangePassword_keeps_the_new_sign_in()
    {
        var session = await SignedInSessionAsync();
        await using var ctx = CreateContext(session);
        _auth.ChangeResult = FormResult.Success(FakeSessionStorage.Login("professor", role: Roles.Professor));
        var cut = ctx.Render<AccountSettings>();
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Signed in as professor (Professor/Scientist)"));

        FillChange(cut, "password", "a new password");

        cut.WaitForAssertion(() => cut.Find("[data-password-changed]").TextContent.Should().Contain("Your password was changed."));
        _auth.LastChange.Should().Be(("password", "a new password"));
        session.IsSignedIn.Should().BeTrue();
        cut.FindAll("input[type=password]").Should().OnlyContain(i => string.IsNullOrEmpty(i.GetAttribute("value")),
            "the form is cleared");
    }

    [Fact]
    public async Task ChangePassword_shows_the_servers_answer_next_to_the_field()
    {
        var session = await SignedInSessionAsync();
        await using var ctx = CreateContext(session);
        _auth.ChangeResult = FormResult.Invalid(new Dictionary<string, string[]> { ["currentPassword"] = ["That isn't your current password."] });
        var cut = ctx.Render<AccountSettings>();
        cut.WaitForAssertion(() => cut.FindAll("input[type=password]").Should().HaveCount(3));

        FillChange(cut, "wrong", "a new password");

        cut.WaitForAssertion(() => cut.Find("[data-field-error=currentPassword]").TextContent.Trim()
            .Should().Be("That isn't your current password."));
        cut.FindAll("[data-password-changed]").Should().BeEmpty();
    }

    [Fact]
    public async Task ChangePassword_checks_the_form_before_sending()
    {
        var session = await SignedInSessionAsync();
        await using var ctx = CreateContext(session);
        var cut = ctx.Render<AccountSettings>();
        cut.WaitForAssertion(() => cut.FindAll("input[type=password]").Should().HaveCount(3));

        FillChange(cut, "", "a new password", "another password");

        cut.Find("[data-field-error=currentPassword]").TextContent.Trim().Should().Be("Enter your current password.");
        cut.Find("[data-field-error=confirmPassword]").TextContent.Trim().Should().Be("The passwords don't match.");
        _auth.LastChange.Should().BeNull();
    }

    [Fact]
    public async Task ChangePassword_asks_signed_out_visitors_to_sign_in()
    {
        await using var ctx = CreateContext();

        var cut = ctx.Render<AccountSettings>();

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Sign in to change your password."));
        cut.FindAll("input[type=password]").Should().BeEmpty();
    }

    [Fact]
    public async Task SignUp_asks_the_person_to_open_the_emailed_link()
    {
        await using var ctx = CreateContext();
        _auth.SignUpResult = new SignUpResult(AccountStatuses.Pending, new Dictionary<string, string[]>(), null, EmailVerificationRequired: true);
        var cut = ctx.Render<SignUp>();
        cut.Find("input[autocomplete=name]").Change("Ada Lovelace");
        cut.Find("input[autocomplete=email]").Change("ada@lab.example");
        cut.FindAll("input[type=password]")[0].Change("analytical");
        cut.FindAll("input[type=password]")[1].Change("analytical");
        cut.Find("input[type=date]").Change("1990-12-10");
        cut.Find("input[autocomplete=organization]").Change("Analytical Engines Lab");
        cut.Find("textarea").Change("Surveys for my lab's studies.");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => cut.Find("[data-signup-done]").TextContent.Should()
            .Contain("We sent a link to ada@lab.example").And.Contain("an administrator will review your sign-up"));
    }

    [Fact]
    public async Task Outbox_shows_each_email_with_clickable_links()
    {
        await using var ctx = CreateContext();
        _accounts.Outbox = [new SentEmail
        {
            To = "ada@lab.example",
            Subject = "Reset your FormFlow password",
            Body = "Open <this> link:\n\nhttp://localhost:5224/reset-password?token=abc",
            SentAt = DateTime.UtcNow,
        }];

        var cut = ctx.Render<AdminOutbox>();

        cut.WaitForAssertion(() => cut.FindAll("[data-outbox-email]").Should().ContainSingle());
        cut.Find("[data-outbox-email] a").GetAttribute("href").Should().Be("http://localhost:5224/reset-password?token=abc");
        cut.Markup.Should().Contain("To ada@lab.example").And.Contain("Open &lt;this&gt; link", "the body is encoded");
    }

    [Fact]
    public async Task Outbox_explains_when_emails_go_through_a_mail_server()
    {
        await using var ctx = CreateContext();
        _accounts.Outbox = null;

        var cut = ctx.Render<AdminOutbox>();

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("FormFlow sends emails through your mail server"));
    }

    [Fact]
    public async Task Outbox_is_for_administrators_only()
    {
        await using var ctx = CreateContext();

        var cut = ctx.Render<AdminOutbox>(p => p.AddCascadingValue(new AdminAccess(Roles.Professor, Guid.NewGuid())));

        cut.Markup.Should().Contain("Only administrators can see the emails FormFlow sends.");
        _accounts.OutboxLoads.Should().Be(0);
    }

    private sealed class OutboxAccounts : IAccountService
    {
        public List<SentEmail>? Outbox { get; set; } = [];
        public int OutboxLoads { get; private set; }

        public Task<List<SentEmail>?> GetOutboxAsync()
        {
            OutboxLoads++;
            return Task.FromResult(Outbox);
        }

        public Task<List<PendingAccount>> GetPendingAsync() => Task.FromResult(new List<PendingAccount>());
        public Task<string?> ApproveAsync(Guid id) => Task.FromResult<string?>(null);
        public Task<string?> DeclineAsync(Guid id) => Task.FromResult<string?>(null);
    }
}
