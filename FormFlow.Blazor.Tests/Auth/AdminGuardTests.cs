using Bunit;
using FluentAssertions;
using FormFlow.Blazor.Components.Layout;
using FormFlow.Blazor.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;

namespace FormFlow.Blazor.Tests.Auth;

public class AdminGuardTests
{
    private readonly FakeSessionStorage _storage = new();

    private BunitContext CreateContext(string path, out AdminSession session, bool interactive = true)
    {
        var ctx = new BunitContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        ctx.Services.AddMudServices();
        session = _storage.CreateSession();
        ctx.Services.AddSingleton(session);
        ctx.SetRendererInfo(new RendererInfo("Server", interactive));
        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo(path);
        return ctx;
    }

    private static IRenderedComponent<AdminGuard> RenderGuard(BunitContext ctx) =>
        ctx.Render<AdminGuard>(p => p.AddChildContent("<p id='secret'>admin page</p>"));

    [Fact]
    public async Task Signed_out_admin_page_redirects_to_sign_in()
    {
        await using var ctx = CreateContext("/admin/surveys?tab=1", out _);
        var nav = ctx.Services.GetRequiredService<NavigationManager>();

        var cut = RenderGuard(ctx);

        cut.WaitForAssertion(() => nav.Uri.Should().EndWith("/login?returnUrl=%2Fadmin%2Fsurveys%3Ftab%3D1"));
    }

    [Fact]
    public async Task Prerendered_admin_page_waits_for_the_sign_in_check()
    {
        await using var ctx = CreateContext("/admin/surveys", out _, interactive: false);
        var nav = ctx.Services.GetRequiredService<NavigationManager>();

        var cut = RenderGuard(ctx);

        cut.FindAll("#secret").Should().BeEmpty();
        cut.Find("[aria-label='Checking sign-in']").Should().NotBeNull();
        nav.Uri.Should().EndWith("/admin/surveys");
    }

    [Fact]
    public async Task Signed_in_admin_sees_the_page()
    {
        await using var ctx = CreateContext("/admin/questions", out var session);
        await session.SignInAsync(FakeSessionStorage.Login());

        var cut = RenderGuard(ctx);

        cut.Find("#secret").TextContent.Should().Be("admin page");
    }

    [Fact]
    public async Task Public_pages_are_not_guarded()
    {
        await using var ctx = CreateContext("/surveys", out _);
        var nav = ctx.Services.GetRequiredService<NavigationManager>();

        var cut = RenderGuard(ctx);

        cut.Find("#secret").Should().NotBeNull();
        nav.Uri.Should().EndWith("/surveys");
    }

    [Fact]
    public async Task Signing_out_hides_the_admin_page()
    {
        await using var ctx = CreateContext("/admin/questions", out var session);
        await session.SignInAsync(FakeSessionStorage.Login());
        var cut = RenderGuard(ctx);

        await cut.InvokeAsync(session.SignOutAsync);

        cut.WaitForAssertion(() => cut.FindAll("#secret").Should().BeEmpty());
    }
}
