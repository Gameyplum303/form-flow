using Bunit;
using FluentAssertions;
using FormFlow.Blazor.Components.Pages;
using FormFlow.Blazor.Services;
using FormFlow.Data.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;

namespace FormFlow.Blazor.Tests.Auth;

public class LoginPageTests
{
    private readonly FakeSessionStorage _storage = new();
    private readonly FakeAuthService _auth = new();

    private BunitContext CreateContext(string? loginHint = null)
    {
        var ctx = new BunitContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        ctx.Services.AddMudServices();
        ctx.Services.AddSingleton<IAuthService>(_auth);
        ctx.Services.AddSingleton(_storage.CreateSession());
        ctx.Services.AddSingleton<IConfiguration>(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["LoginHint"] = loginHint })
            .Build());
        return ctx;
    }

    private static void SignIn(IRenderedComponent<Login> cut, string username, string password)
    {
        cut.Find("input[autocomplete=username]").Change(username);
        cut.Find("input[type=password]").Change(password);
        cut.Find("form").Submit();
    }

    [Fact]
    public async Task Valid_credentials_sign_in_and_return_to_the_page_that_asked()
    {
        await using var ctx = CreateContext();
        var nav = ctx.Services.GetRequiredService<NavigationManager>();
        nav.NavigateTo("/login?returnUrl=%2Fadmin%2Fquestions");

        var cut = ctx.Render<Login>();
        SignIn(cut, " admin ", "secret");

        cut.WaitForAssertion(() => nav.Uri.Should().EndWith("/admin/questions"));
        _auth.LastAttempt.Should().Be(("admin", "secret"));
        ctx.Services.GetRequiredService<AdminSession>().Username.Should().Be("admin");
    }

    [Fact]
    public async Task Wrong_password_shows_the_error_and_stays_signed_out()
    {
        await using var ctx = CreateContext();
        _auth.Result = (null, "Invalid username or password.");

        var cut = ctx.Render<Login>();
        SignIn(cut, "admin", "nope");

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Invalid username or password."));
        ctx.Services.GetRequiredService<AdminSession>().IsSignedIn.Should().BeFalse();
    }

    [Fact]
    public async Task Shows_the_configured_hint()
    {
        await using var ctx = CreateContext(loginHint: "Demo: admin / formflow-admin");

        var cut = ctx.Render<Login>();

        cut.Markup.Should().Contain("Demo: admin / formflow-admin");
    }

    [Theory]
    [InlineData("/admin/surveys/1/results", "/admin/surveys/1/results")]
    [InlineData(null, "/admin/surveys")]
    [InlineData("", "/admin/surveys")]
    [InlineData("https://evil.example", "/admin/surveys")]
    [InlineData("//evil.example", "/admin/surveys")]
    [InlineData("/\\evil.example", "/admin/surveys")]
    public void SafeReturnUrl_only_allows_local_paths(string? returnUrl, string expected)
    {
        Login.SafeReturnUrl(returnUrl).Should().Be(expected);
    }

    private sealed class FakeAuthService : IAuthService
    {
        public (LoginResponse? Login, string? Error) Result { get; set; } = (FakeSessionStorage.Login(), null);
        public (string Username, string Password)? LastAttempt { get; private set; }

        public Task<(LoginResponse? Login, string? Error)> LoginAsync(string username, string password)
        {
            LastAttempt = (username, password);
            return Task.FromResult(Result);
        }
    }
    [Fact]
    public async Task Students_go_to_the_survey_list_instead_of_the_builder()
    {
        await using var ctx = CreateContext();
        _auth.Result = (FakeSessionStorage.Login("student", role: Roles.Student), null);
        var nav = ctx.Services.GetRequiredService<NavigationManager>();
        nav.NavigateTo("/login?returnUrl=%2Fadmin%2Fsurveys");

        var cut = ctx.Render<Login>();
        SignIn(cut, "student", "password");

        cut.WaitForAssertion(() => nav.Uri.Should().EndWith("/surveys"));
        nav.Uri.Should().NotContain("/admin");
    }

    [Theory]
    [InlineData(null, true, "/admin/surveys")]
    [InlineData(null, false, "/surveys")]
    [InlineData("/admin/questions", false, "/surveys")]
    [InlineData("/surveys/123", false, "/surveys/123")]
    [InlineData("/admin/questions", true, "/admin/questions")]
    public void Destination_depends_on_whether_the_account_builds_surveys(string? returnUrl, bool canBuild, string expected)
    {
        Login.Destination(returnUrl, canBuild).Should().Be(expected);
    }
}
