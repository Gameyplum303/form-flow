using Bunit;
using FluentAssertions;
using FormFlow.Blazor.Components.Pages;
using FormFlow.Blazor.Services;
using FormFlow.Data.Models;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;

namespace FormFlow.Blazor.Tests.Auth;

public class SignUpPageTests
{
    private readonly FakeAuthService _auth = new();

    private BunitContext CreateContext()
    {
        var ctx = new BunitContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        ctx.Services.AddMudServices();
        ctx.Services.AddSingleton<IAuthService>(_auth);
        return ctx;
    }

    private static void Fill(IRenderedComponent<SignUp> cut, string password = "analytical", string? confirm = null,
        string dateOfBirth = "1990-12-10")
    {
        cut.Find("input[autocomplete=name]").Change("Ada Lovelace");
        cut.Find("input[autocomplete=email]").Change("ada@lab.example");
        cut.FindAll("input[type=password]")[0].Change(password);
        cut.FindAll("input[type=password]")[1].Change(confirm ?? password);
        cut.Find("input[type=date]").Change(dateOfBirth);
        cut.Find("input[autocomplete=organization]").Change("Analytical Engines Lab");
        cut.Find("textarea").Change("Surveys for my lab's studies.");
    }

    private static IEnumerable<string> ErrorsFor(IRenderedComponent<SignUp> cut, string field) =>
        cut.FindAll($"[data-field-error={field}]").Select(e => e.TextContent.Trim());

    [Fact]
    public async Task Asks_for_every_detail_including_the_organization()
    {
        await using var ctx = CreateContext();

        var cut = ctx.Render<SignUp>();

        cut.Markup.Should().Contain("Sign up as a Professor/Scientist");
        foreach (var label in new[] { "Full name", "Email", "Password", "Confirm password", "Date of birth", "Organization", "How will you use FormFlow?" })
        {
            cut.Markup.Should().Contain(label);
        }
        cut.Markup.Should().Contain("Students don't need an account");
    }

    [Fact]
    public async Task Complete_form_signs_up_and_says_an_administrator_will_review_it()
    {
        await using var ctx = CreateContext();
        var cut = ctx.Render<SignUp>();

        Fill(cut);
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => cut.Find("[data-signup-done]").TextContent.Should().Contain("An administrator will review your sign-up"));
        var request = _auth.SignUps.Should().ContainSingle().Subject;
        request.Email.Should().Be("ada@lab.example");
        request.DateOfBirth.Should().Be("1990-12-10");
        request.Organization.Should().Be("Analytical Engines Lab");
        request.IntendedUse.Should().Be("Surveys for my lab's studies.");
        cut.Find("a[href='/login']").TextContent.Should().Contain("Go to sign in");
    }

    [Fact]
    public async Task Without_approval_the_account_is_ready_straight_away()
    {
        await using var ctx = CreateContext();
        _auth.SignUpResult = new SignUpResult(AccountStatuses.Active, new Dictionary<string, string[]>(), null);
        var cut = ctx.Render<SignUp>();

        Fill(cut);
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => cut.Find("[data-signup-done]").TextContent.Should().Contain("Your account is ready"));
    }

    [Fact]
    public async Task Mistakes_show_under_their_fields_without_calling_the_server()
    {
        await using var ctx = CreateContext();
        var cut = ctx.Render<SignUp>();

        Fill(cut, password: "short", confirm: "different", dateOfBirth: DateTime.Today.AddYears(-10).ToString("yyyy-MM-dd"));
        cut.Find("input[autocomplete=email]").Change("ada");
        cut.Find("form").Submit();

        ErrorsFor(cut, "password").Should().Equal("Use at least 8 characters.");
        ErrorsFor(cut, "confirmPassword").Should().Equal("The passwords don't match.");
        ErrorsFor(cut, "dateOfBirth").Should().Equal("You must be at least 18 to sign up.");
        ErrorsFor(cut, "email").Should().Equal("Enter an email address, like name@example.com.");
        _auth.SignUps.Should().BeEmpty();
    }

    [Fact]
    public async Task Server_errors_show_under_their_fields()
    {
        await using var ctx = CreateContext();
        _auth.SignUpResult = new SignUpResult(null,
            new Dictionary<string, string[]> { ["email"] = ["An account with this email already exists. Try signing in."] }, null);
        var cut = ctx.Render<SignUp>();

        Fill(cut);
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => ErrorsFor(cut, "email").Should().Equal("An account with this email already exists. Try signing in."));
        cut.FindAll("[data-signup-done]").Should().BeEmpty();
    }

    [Fact]
    public async Task A_general_failure_shows_a_message()
    {
        await using var ctx = CreateContext();
        _auth.SignUpResult = SignUpResult.Failed("Could not reach the server. Please try again.");
        var cut = ctx.Render<SignUp>();

        Fill(cut);
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Could not reach the server. Please try again."));
    }
}
