using Bunit;
using FluentAssertions;
using FormFlow.Blazor.Components.Pages.Admin;
using FormFlow.Blazor.Services;
using FormFlow.Blazor.Tests.Respond;
using FormFlow.Data.Models;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;

namespace FormFlow.Blazor.Tests.Admin;

public class AdminSurveyShareTests
{
    private readonly FakeSurveyService _service = new();
    private readonly SurveyDefinition _survey = new()
    {
        Id = Guid.NewGuid(),
        Title = "Lab feedback",
        Description = "About the lab",
        QuestionIds = [Guid.NewGuid()],
        CreatedAt = DateTime.UtcNow,
        Status = SurveyStatuses.Draft,
        Listed = false,
        ShareCode = "k7m2p9qa",
    };

    public AdminSurveyShareTests() => _service.Surveys.Add(_survey);

    private BunitContext CreateContext(int timezoneOffset = 0, bool copyWorks = true)
    {
        var ctx = new BunitContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        ctx.JSInterop.Setup<int>("formFlow.timezoneOffset").SetResult(timezoneOffset);
        ctx.JSInterop.Setup<bool>("formFlow.copyText", _ => true).SetResult(copyWorks);
        ctx.Services.AddMudServices();
        ctx.Services.AddSingleton<ISurveyService>(_service);
        ctx.Render<MudPopoverProvider>();
        return ctx;
    }

    private IRenderedComponent<AdminSurveyShare> RenderPage(BunitContext ctx)
    {
        var cut = ctx.Render<AdminSurveyShare>(p => p.Add(x => x.Id, _survey.Id));
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Lab feedback"));
        return cut;
    }

    private static Task ClickAsync(IRenderedComponent<AdminSurveyShare> cut, string label) =>
        cut.InvokeAsync(() => cut.FindAll("button").First(b => b.TextContent.Trim() == label).Click());

    [Fact]
    public async Task ShowsTheShareLinkAndAQrCodeForIt()
    {
        await using var ctx = CreateContext();
        var cut = RenderPage(ctx);

        var link = cut.Find("[data-share-link]");
        (link.TagName == "INPUT" ? link : link.QuerySelector("input")!).GetAttribute("value")
            .Should().Be("http://localhost/s/k7m2p9qa");
        cut.Find("[data-share-qr] svg").Should().NotBeNull();
        cut.Find("[data-survey-status]").GetAttribute("data-survey-status").Should().Be("Draft");
        cut.Markup.Should().Contain("The link works for everyone once you publish the survey.");
    }

    [Fact]
    public async Task Publishing_SavesTheSettings_AndUpdatesTheStatus()
    {
        await using var ctx = CreateContext();
        var snackbars = ctx.Render<MudSnackbarProvider>();
        var cut = RenderPage(ctx);

        await cut.InvokeAsync(() => cut.FindComponent<MudRadioGroup<string>>().Instance.ValueChanged.InvokeAsync(SurveyStatuses.Published));
        await cut.InvokeAsync(() => cut.FindComponent<MudCheckBox<bool>>().Instance.ValueChanged.InvokeAsync(true));
        await ClickAsync(cut, "Save sharing settings");

        _service.LastSharing!.Value.Id.Should().Be(_survey.Id);
        _service.LastSharing.Value.Sharing.Status.Should().Be(SurveyStatuses.Published);
        _service.LastSharing.Value.Sharing.Listed.Should().BeTrue();
        _service.LastSharing.Value.Sharing.ClosesAt.Should().BeNull();
        snackbars.WaitForAssertion(() => snackbars.Markup.Should().Contain("Saved. The survey is published."));
        cut.WaitForAssertion(() => cut.Find("[data-survey-status]").GetAttribute("data-survey-status").Should().Be("Published"));
        cut.Markup.Should().NotContain("once you publish the survey");
    }

    [Fact]
    public async Task ADraftIsNeverListed_EvenIfTheBoxWasTicked()
    {
        await using var ctx = CreateContext();
        _survey.Listed = true;
        var cut = RenderPage(ctx);

        await ClickAsync(cut, "Save sharing settings");

        _service.LastSharing!.Value.Sharing.Status.Should().Be(SurveyStatuses.Draft);
        _service.LastSharing.Value.Sharing.Listed.Should().BeFalse();
    }

    [Theory]
    [InlineData("2026-10-01T17:30")]
    [InlineData("2026-10-01T17:30:00")] // how Blazor passes on a datetime-local value
    public async Task CloseDate_IsEnteredInLocalTime_AndSavedInUtc(string entered)
    {
        // The browser is five hours behind UTC (getTimezoneOffset() = 300).
        await using var ctx = CreateContext(timezoneOffset: 300);
        var cut = RenderPage(ctx);

        await cut.InvokeAsync(() => cut.Find("#share-closes-at").Change(entered));
        await ClickAsync(cut, "Save sharing settings");

        var closesAt = _service.LastSharing!.Value.Sharing.ClosesAt;
        closesAt.Should().Be(new DateTime(2026, 10, 1, 22, 30, 0, DateTimeKind.Utc));
        closesAt!.Value.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public async Task ExistingCloseDate_IsShownInLocalTime()
    {
        _survey.ClosesAt = new DateTime(2026, 10, 1, 22, 30, 0, DateTimeKind.Utc);
        await using var ctx = CreateContext(timezoneOffset: 300);
        var cut = RenderPage(ctx);

        cut.WaitForAssertion(() =>
            cut.Find("#share-closes-at").GetAttribute("value").Should().Be("2026-10-01T17:30"));
    }

    [Fact]
    public async Task ClearingTheCloseDate_KeepsTheSurveyOpen()
    {
        _survey.ClosesAt = DateTime.UtcNow.AddDays(3);
        await using var ctx = CreateContext();
        var cut = RenderPage(ctx);

        await ClickAsync(cut, "Clear");
        await ClickAsync(cut, "Save sharing settings");

        _service.LastSharing!.Value.Sharing.ClosesAt.Should().BeNull();
    }

    [Fact]
    public async Task RefusedSave_SaysWhy()
    {
        await using var ctx = CreateContext();
        _service.NextSharingError = "Request failed (403)";
        var snackbars = ctx.Render<MudSnackbarProvider>();
        var cut = RenderPage(ctx);

        await ClickAsync(cut, "Save sharing settings");

        snackbars.WaitForAssertion(() => snackbars.Markup.Should().Contain("The sharing settings could not be saved: Request failed (403)"));
    }

    [Theory]
    [InlineData(true, "Link copied.")]
    [InlineData(false, "Your browser blocked copying.")]
    public async Task CopyLink_CopiesTheLink_OrExplainsWhyNot(bool copyWorks, string message)
    {
        await using var ctx = CreateContext(copyWorks: copyWorks);
        var snackbars = ctx.Render<MudSnackbarProvider>();
        var cut = RenderPage(ctx);

        await ClickAsync(cut, "Copy link");

        ctx.JSInterop.VerifyInvoke("formFlow.copyText").Arguments.Should().Equal("http://localhost/s/k7m2p9qa");
        snackbars.WaitForAssertion(() => snackbars.Markup.Should().Contain(message));
    }

    [Fact]
    public async Task Professor_CannotShareSomeoneElsesSurvey()
    {
        await using var ctx = CreateContext();
        var cut = ctx.Render<AdminSurveyShare>(p => p.Add(x => x.Id, _survey.Id)
            .AddCascadingValue(new AdminAccess("professor", Guid.NewGuid())));

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("You can only share surveys you created."));
        cut.FindAll("[data-share-link]").Should().BeEmpty();
    }

    [Fact]
    public async Task UnknownSurvey_SaysSo()
    {
        await using var ctx = CreateContext();
        var cut = ctx.Render<AdminSurveyShare>(p => p.Add(x => x.Id, Guid.NewGuid()));

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Survey not found."));
    }
}
