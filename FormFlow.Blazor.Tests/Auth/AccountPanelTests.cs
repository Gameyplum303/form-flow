using Bunit;
using FluentAssertions;
using FormFlow.Blazor.Components.Layout;
using FormFlow.Blazor.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;

namespace FormFlow.Blazor.Tests.Auth;

public class AccountPanelTests
{
    private readonly FakeSessionStorage _storage = new();

    private BunitContext CreateContext(out AdminSession session)
    {
        var ctx = new BunitContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        ctx.Services.AddMudServices();
        session = _storage.CreateSession();
        ctx.Services.AddSingleton(session);
        return ctx;
    }

    [Theory]
    [InlineData("admin", "Administrator")]
    [InlineData("professor", "Professor/Scientist")]
    [InlineData("student", "Student")]
    public async Task Shows_who_is_signed_in_and_their_role(string role, string name)
    {
        await using var ctx = CreateContext(out var session);
        await session.SignInAsync(FakeSessionStorage.Login("someone", role: role));

        var cut = ctx.Render<AccountPanel>();

        cut.Markup.Should().Contain($"Signed in as someone ({name})");
        cut.FindAll("select").Should().HaveCount(role == "admin" ? 1 : 0, "only administrators can switch views");
    }

    [Theory]
    [InlineData("student", "/surveys")]
    [InlineData("professor", "/admin/surveys")]
    [InlineData("admin", "/admin/surveys")]
    public async Task Admins_can_view_the_site_as_another_role(string role, string landsOn)
    {
        await using var ctx = CreateContext(out var session);
        await session.SignInAsync(FakeSessionStorage.Login("Rogers"));
        var nav = ctx.Services.GetRequiredService<NavigationManager>();
        var cut = ctx.Render<AccountPanel>();

        await cut.Find("select").ChangeAsync(new ChangeEventArgs { Value = role });

        session.EffectiveRole.Should().Be(role);
        session.IsAdmin.Should().BeTrue();
        nav.Uri.Should().EndWith(landsOn);
    }

    [Fact]
    public async Task Offers_sign_in_when_signed_out_if_asked()
    {
        await using var ctx = CreateContext(out _);

        var signedOut = ctx.Render<AccountPanel>(p => p.Add(x => x.ShowSignIn, true));
        signedOut.Find("a[href='/login']").TextContent.Should().Contain("Sign in");
        signedOut.Find("a[href='/signup']").TextContent.Should().Contain("Professor sign-up");
        ctx.Render<AccountPanel>().Markup.Trim().Should().BeEmpty();
    }
}
