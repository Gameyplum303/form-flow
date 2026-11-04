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

public class AdminSurveyPresentTests
{
    private readonly FakeSurveyService _service = new();
    private readonly FakeLiveResultsClient _live = new();
    private readonly SurveyDefinition _survey = new()
    {
        Id = Guid.NewGuid(),
        Title = "Week 3 check-in",
        Description = "How is the course going?",
        QuestionIds = [Guid.NewGuid()],
        CreatedAt = DateTime.UtcNow,
        Status = SurveyStatuses.Published,
        Listed = false,
        ShareCode = "k7m2p9qa",
    };

    public AdminSurveyPresentTests() => _service.Surveys.Add(_survey);

    private BunitContext CreateContext()
    {
        var ctx = new BunitContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        ctx.Services.AddMudServices();
        ctx.Services.AddSingleton<ISurveyService>(_service);
        ctx.Services.AddSingleton<ILiveResultsClient>(_live);
        ctx.Render<MudPopoverProvider>();
        return ctx;
    }

    private IRenderedComponent<AdminSurveyPresent> RenderPage(BunitContext ctx, AdminAccess? access = null)
    {
        var cut = ctx.Render<AdminSurveyPresent>(p =>
        {
            p.Add(x => x.Id, _survey.Id);
            if (access is not null)
            {
                p.AddCascadingValue(access);
            }
        });
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Week 3 check-in"));
        return cut;
    }

    private SurveyResults Results(int total, int yes, int no) => new()
    {
        SurveyId = _survey.Id,
        Title = _survey.Title,
        TotalResponses = total,
        Questions =
        [
            new QuestionResult
            {
                Key = "keeping_up", Label = "Are you keeping up?", Type = QuestionTypes.YesNo, AnsweredCount = yes + no,
                Options = [new OptionCount { Value = "true", Label = "Yes", Count = yes }, new OptionCount { Value = "false", Label = "No", Count = no }],
            },
            new QuestionResult
            {
                Key = "comments", Label = "Anything else?", Type = QuestionTypes.LongText, AnsweredCount = 1,
                RecentAnswers = ["The labs are too long"],
            },
        ],
    };

    [Fact]
    public async Task ShowsHowToJoin_InBigLetters()
    {
        await using var ctx = CreateContext();
        var cut = RenderPage(ctx);

        cut.Find("[data-join-address]").TextContent.Should().Be("localhost/join");
        var code = cut.Find("[data-share-code]");
        code.GetAttribute("data-share-code").Should().Be("k7m2p9qa");
        code.TextContent.Should().Be("K7M2 P9QA");
        cut.Find(".presenter-qr svg").Should().NotBeNull();
        cut.Find("[data-share-qr]").GetAttribute("aria-label").Should().Be("QR code for http://localhost/s/k7m2p9qa");
    }

    [Fact]
    public async Task UpdatesTheCountAndCharts_AsResultsArrive()
    {
        await using var ctx = CreateContext();
        var cut = RenderPage(ctx);
        cut.Find("[data-connection]").GetAttribute("data-connection").Should().Be("connecting");
        cut.Markup.Should().Contain("Waiting for the results");

        _live.Send(Results(total: 2, yes: 1, no: 1));
        cut.WaitForAssertion(() => cut.Find("[data-response-count]").GetAttribute("data-response-count").Should().Be("2"));
        cut.Find("[data-connection]").GetAttribute("data-connection").Should().Be("live");
        cut.Find("[data-live-result=keeping_up] [data-option-bar=Yes]").TextContent.Should().Contain("1 (50%)");

        _live.Send(Results(total: 3, yes: 2, no: 1));
        cut.WaitForAssertion(() => cut.Find("[data-response-count]").GetAttribute("data-response-count").Should().Be("3"));
        cut.Find("[data-live-result=keeping_up] [data-option-bar=Yes]").TextContent.Should().Contain("2 (67%)");
    }

    [Fact]
    public async Task KeepsWrittenAnswersOffTheProjector()
    {
        await using var ctx = CreateContext();
        var cut = RenderPage(ctx);

        _live.Send(Results(total: 1, yes: 1, no: 0));

        cut.WaitForAssertion(() => cut.Find("[data-live-result=comments]").TextContent.Should().Contain("Written answers are on the results page."));
        cut.Markup.Should().NotContain("The labs are too long");
    }

    [Fact]
    public async Task ShowsWhenTheStreamDrops_AndReconnects()
    {
        await using var ctx = CreateContext();
        var cut = RenderPage(ctx);
        _live.Send(Results(total: 1, yes: 1, no: 0));
        cut.WaitForAssertion(() => cut.Find("[data-connection]").GetAttribute("data-connection").Should().Be("live"));

        _live.Drop();
        cut.WaitForAssertion(() => cut.Find("[data-connection]").GetAttribute("data-connection").Should().Be("reconnecting"));
        // The last results stay up while it reconnects.
        cut.Find("[data-response-count]").GetAttribute("data-response-count").Should().Be("1");

        // The update waits for the next connection, which opens after a pause.
        _live.Send(Results(total: 2, yes: 2, no: 0));
        cut.WaitForAssertion(() => cut.Find("[data-connection]").GetAttribute("data-connection").Should().Be("live"),
            TimeSpan.FromSeconds(5));
        cut.Find("[data-response-count]").GetAttribute("data-response-count").Should().Be("2");
        _live.Connections.Should().Be(2);
    }

    [Fact]
    public async Task ARefusedStream_SaysWhy()
    {
        _live.RefuseWith(new LiveResultsRefusedException(System.Net.HttpStatusCode.Unauthorized));
        await using var ctx = CreateContext();
        var cut = RenderPage(ctx);

        cut.WaitForAssertion(() => cut.Find("[data-connection]").GetAttribute("data-connection").Should().Be("stopped"));
        cut.Markup.Should().Contain("Your sign-in has ended.");
    }

    [Fact]
    public async Task ADraft_CanBePublishedFromThePresenterView()
    {
        _survey.Status = SurveyStatuses.Draft;
        _survey.ShowResultsToRespondents = true;
        await using var ctx = CreateContext();
        var cut = RenderPage(ctx);
        cut.Find("[data-presenter-notice]").TextContent.Should().Contain("This survey is a draft");

        await cut.InvokeAsync(() => cut.FindAll("button").First(b => b.TextContent.Trim() == "Publish now").Click());

        var sharing = _service.LastSharing!.Value.Sharing;
        sharing.Status.Should().Be(SurveyStatuses.Published);
        sharing.ShowResultsToRespondents.Should().BeTrue();
        cut.WaitForAssertion(() => cut.FindAll("[data-presenter-notice]").Should().BeEmpty());
    }

    [Fact]
    public async Task AClosedSurvey_CanBeReopened()
    {
        _survey.ClosesAt = DateTime.UtcNow.AddHours(-1);
        await using var ctx = CreateContext();
        var cut = RenderPage(ctx);
        cut.Find("[data-presenter-notice]").TextContent.Should().Contain("This survey has closed");
        cut.Find("[data-presenter-notice] a").GetAttribute("href").Should().Be($"/admin/surveys/{_survey.Id}/share");

        await cut.InvokeAsync(() => cut.FindAll("button").First(b => b.TextContent.Trim() == "Reopen now").Click());

        _service.LastSharing!.Value.Sharing.ClosesAt.Should().BeNull();
        cut.WaitForAssertion(() => cut.FindAll("[data-presenter-notice]").Should().BeEmpty());
    }

    [Fact]
    public async Task Professor_CannotPresentSomeoneElsesSurvey()
    {
        await using var ctx = CreateContext();
        var cut = ctx.Render<AdminSurveyPresent>(p => p.Add(x => x.Id, _survey.Id)
            .AddCascadingValue(new AdminAccess(Roles.Professor, Guid.NewGuid())));

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("You can only present surveys you created."));
        cut.FindAll("[data-share-code]").Should().BeEmpty();
        _live.Connections.Should().Be(0);
    }

    [Fact]
    public async Task UnknownSurvey_SaysSo()
    {
        await using var ctx = CreateContext();
        var cut = ctx.Render<AdminSurveyPresent>(p => p.Add(x => x.Id, Guid.NewGuid()));

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Survey not found."));
    }

    [Fact]
    public async Task LeavingThePage_ClosesTheStream()
    {
        await using var ctx = CreateContext();
        var cut = RenderPage(ctx);
        cut.WaitForAssertion(() => _live.Connections.Should().Be(1));

        cut.Instance.Dispose();
        _live.Drop();
        await Task.Delay(1500);

        // A closed page doesn't reconnect.
        _live.Connections.Should().Be(1);
    }
}
