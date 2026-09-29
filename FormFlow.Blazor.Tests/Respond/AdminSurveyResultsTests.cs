using Bunit;
using FluentAssertions;
using FormFlow.Blazor.Components.Pages.Admin;
using FormFlow.Blazor.Services;
using FormFlow.Data.Models;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;

namespace FormFlow.Blazor.Tests.Respond;

public class AdminSurveyResultsTests
{
    private readonly FakeSurveyService _service = new();

    private BunitContext CreateContext()
    {
        var ctx = new BunitContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        ctx.Services.AddMudServices();
        ctx.Services.AddSingleton<ISurveyService>(_service);
        return ctx;
    }

    [Fact]
    public async Task RendersCountsAndNumbers()
    {
        await using var ctx = CreateContext();
        var id = Guid.NewGuid();
        _service.Results = new SurveyResults
        {
            SurveyId = id,
            Title = "Campus survey",
            TotalResponses = 4,
            MatchingResponses = 4,
            Questions =
            [
                new QuestionResult
                {
                    Key = "campus", Label = "Which campus?", Type = "radio", AnsweredCount = 4,
                    Options = [new OptionCount { Value = "north", Label = "North", Count = 3 }, new OptionCount { Value = "south", Label = "South", Count = 1 }]
                },
                new QuestionResult
                {
                    Key = "age", Label = "Age", Type = "number", AnsweredCount = 4,
                    Numbers = new NumberSummary { Min = 18, Max = 40, Average = 27.5m }
                }
            ]
        };

        var cut = ctx.Render<AdminSurveyResults>(p => p.Add(x => x.Id, id));

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("4 responses"));
        cut.Markup.Should().Contain("3 (75%)");
        cut.Markup.Should().Contain("27.5");
    }

    [Fact]
    public async Task Ratings_ShowTheAverageAboveTheStarCounts()
    {
        await using var ctx = CreateContext();
        var id = Guid.NewGuid();
        _service.Results = new SurveyResults
        {
            SurveyId = id,
            Title = "Campus survey",
            TotalResponses = 2,
            MatchingResponses = 2,
            Questions =
            [
                new QuestionResult
                {
                    Key = "stars", Label = "Rate it", Type = "rating", AnsweredCount = 2,
                    Options = Enumerable.Range(1, 5).Select(n => new OptionCount { Value = $"{n}", Label = $"{n} stars", Count = n >= 4 ? 1 : 0 }).ToList(),
                    Numbers = new NumberSummary { Min = 4, Max = 5, Average = 4.5m }
                }
            ]
        };

        var cut = ctx.Render<AdminSurveyResults>(p => p.Add(x => x.Id, id));

        cut.WaitForAssertion(() => cut.Find("[data-average-rating]").TextContent.Should().Contain("Average rating: 4.5 of 5"));
        cut.Markup.Should().Contain("5 stars");
    }

    [Fact]
    public async Task DownloadCsv_saves_the_file_through_the_browser()
    {
        await using var ctx = CreateContext();
        var id = Guid.NewGuid();
        _service.Results = new SurveyResults { SurveyId = id, Title = "Campus survey", TotalResponses = 1, MatchingResponses = 1 };
        _service.Export = new CsvExport("campus-survey-responses.csv", [1, 2, 3]);
        var download = ctx.JSInterop.SetupVoid("formFlow.downloadFile", _ => true);

        var cut = ctx.Render<AdminSurveyResults>(p => p.Add(x => x.Id, id));
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("1 response"));
        cut.FindAll("button").Single(b => b.TextContent.Contains("Download CSV")).Click();

        cut.WaitForAssertion(() => download.Invocations["formFlow.downloadFile"].Should().ContainSingle());
        var args = download.Invocations["formFlow.downloadFile"][0].Arguments;
        args[0].Should().Be("campus-survey-responses.csv");
        args[1].Should().Be("text/csv");
        ((byte[])args[2]!).Should().Equal(1, 2, 3);
    }

    [Fact]
    public async Task DownloadCsv_reports_a_failed_download()
    {
        await using var ctx = CreateContext();
        var id = Guid.NewGuid();
        _service.Results = new SurveyResults { SurveyId = id, Title = "Campus survey", TotalResponses = 1, MatchingResponses = 1 };
        var cut = ctx.Render<AdminSurveyResults>(p => p.Add(x => x.Id, id));
        ctx.Render<MudBlazor.MudSnackbarProvider>();
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("1 response"));

        cut.FindAll("button").Single(b => b.TextContent.Contains("Download CSV")).Click();

        ctx.Services.GetRequiredService<MudBlazor.ISnackbar>().ShownSnackbars
            .Should().ContainSingle(s => s.Message == "The CSV could not be downloaded.");
    }

    [Fact]
    public async Task MissingSurvey_ShowsNotFound()
    {
        await using var ctx = CreateContext();
        var cut = ctx.Render<AdminSurveyResults>(p => p.Add(x => x.Id, Guid.NewGuid()));

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Survey not found"));
    }
}
