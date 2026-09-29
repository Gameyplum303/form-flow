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

    [Theory]
    [InlineData("/admin/surveys")]
    [InlineData("/admin/questions/create")]
    [InlineData("/admin/surveys/11111111-1111-1111-1111-111111111111/results")]
    public async Task Students_cannot_open_the_survey_builder(string path)
    {
        await using var ctx = CreateContext(path, out var session);
        await session.SignInAsync(FakeSessionStorage.Login("student", role: "student"));

        var cut = RenderGuard(ctx);

        cut.FindAll("#secret").Should().BeEmpty();
        cut.Markup.Should().Contain("Students can take surveys but can't open the survey builder.");
    }

    [Fact]
    public async Task Professors_get_the_builder_with_their_own_access()
    {
        var id = Guid.NewGuid();
        await using var ctx = CreateContext("/admin/surveys", out var session);
        await session.SignInAsync(FakeSessionStorage.Login("professor", role: "professor", userId: id));

        var cut = ctx.Render<AdminGuard>(p => p.AddChildContent<AccessProbe>());

        cut.Find("#access").TextContent.Should().Be($"professor {id}");
        cut.FindAll("[data-view-as-banner]").Should().BeEmpty();
    }

    [Fact]
    public async Task Admin_previewing_the_professor_view_gets_professor_access_and_a_way_back()
    {
        var id = Guid.NewGuid();
        await using var ctx = CreateContext("/admin/surveys", out var session);
        await session.SignInAsync(FakeSessionStorage.Login("Rogers", userId: id));
        await session.SetViewAsAsync("professor");
        var nav = ctx.Services.GetRequiredService<NavigationManager>();

        var cut = ctx.Render<AdminGuard>(p => p.AddChildContent<AccessProbe>());

        cut.Find("#access").TextContent.Should().Be($"professor {id}");
        cut.Find("[data-view-as-banner]").TextContent.Should().Contain("You're viewing the site as a Professor/Scientist.");

        await cut.InvokeAsync(() => cut.FindAll("button").Single(b => b.TextContent.Contains("Back to Administrator view")).Click());

        cut.WaitForAssertion(() => cut.Find("#access").TextContent.Should().Be($"admin {id}"));
        session.ViewAs.Should().BeNull();
        nav.Uri.Should().EndWith("/admin/surveys");
    }

    [Fact]
    public async Task Admin_previewing_the_student_view_is_kept_out_of_the_builder()
    {
        await using var ctx = CreateContext("/admin/questions", out var session);
        await session.SignInAsync(FakeSessionStorage.Login("Rogers"));
        await session.SetViewAsAsync("student");

        var cut = RenderGuard(ctx);

        cut.FindAll("#secret").Should().BeEmpty();
        cut.Markup.Should().Contain("You're viewing the site as a Student.");
    }

    [Fact]
    public async Task The_preview_survives_a_reload_but_only_for_an_admin()
    {
        var protection = new Microsoft.AspNetCore.DataProtection.EphemeralDataProtectionProvider();
        var admin = _storage.CreateSession(protection);
        await admin.SignInAsync(FakeSessionStorage.Login("Rogers"));
        await admin.SetViewAsAsync("student");

        var reloaded = _storage.CreateSession(protection);
        await reloaded.RestoreAsync();
        reloaded.EffectiveRole.Should().Be("student");
        reloaded.IsAdmin.Should().BeTrue("the preview only changes what the pages show");

        var professor = new FakeSessionStorage().CreateSession();
        await professor.SignInAsync(FakeSessionStorage.Login("professor", role: "professor"));
        await professor.SetViewAsAsync("student");
        professor.ViewAs.Should().BeNull();
        professor.EffectiveRole.Should().Be("professor");
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

    /// <summary>Shows the access the guard cascades to admin pages.</summary>
    private sealed class AccessProbe : ComponentBase
    {
        [CascadingParameter] public AdminAccess? Access { get; set; }

        protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder)
        {
            builder.OpenElement(0, "span");
            builder.AddAttribute(1, "id", "access");
            builder.AddContent(2, Access is null ? "none" : $"{Access.Role} {Access.UserId}");
            builder.CloseElement();
        }
    }
}
