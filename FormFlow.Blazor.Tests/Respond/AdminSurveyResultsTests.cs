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
    public async Task ScaleQuestions_ShowGridRows_TheNpsScore_AndSliderNumbers()
    {
        await using var ctx = CreateContext();
        var id = Guid.NewGuid();
        var scale = new[] { ("1", "Disagree"), ("2", "Agree") };
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
                    Key = "services", Label = "How much do you agree?", Type = "likert", AnsweredCount = 4,
                    Rows =
                    [
                        new RowResult
                        {
                            Value = "library", Label = "The library is great", AnsweredCount = 4, Average = 1.75m,
                            Options = scale.Select((o, i) => new OptionCount { Value = o.Item1, Label = o.Item2, Count = i == 0 ? 1 : 3 }).ToList()
                        },
                        new RowResult
                        {
                            Value = "labs", Label = "The labs are new", AnsweredCount = 0,
                            Options = scale.Select(o => new OptionCount { Value = o.Item1, Label = o.Item2 }).ToList()
                        }
                    ]
                },
                new QuestionResult
                {
                    Key = "recommend", Label = "Would you recommend us?", Type = "nps", AnsweredCount = 4,
                    Options = Enumerable.Range(0, 11).Select(n => new OptionCount { Value = $"{n}", Label = $"{n}", Count = n is 10 or 9 or 3 or 7 ? 1 : 0 }).ToList(),
                    Nps = new NpsSummary { Score = 25, Promoters = 2, Passives = 1, Detractors = 1 }
                },
                new QuestionResult
                {
                    Key = "hours", Label = "Hours of study", Type = "slider", AnsweredCount = 4,
                    Numbers = new NumberSummary { Min = 10, Max = 40, Average = 25 }
                }
            ]
        };

        var cut = ctx.Render<AdminSurveyResults>(p => p.Add(x => x.Id, id));

        cut.WaitForAssertion(() => cut.Find("[data-nps-score]").TextContent.Should().Be("+25"));
        cut.Find("[data-nps]").TextContent.Should().Contain("Promoters (9–10)").And.Contain("Detractors (0–6)");
        cut.Find("[data-result-key='recommend']").TextContent.Should().Contain("NPS (0–10)");

        var library = cut.Find("[data-likert-result-row='library']").TextContent;
        library.Should().Contain("The library is great").And.Contain("1 (25%)").And.Contain("3 (75%)").And.Contain("1.75");
        cut.Find("[data-likert-result-row='labs']").TextContent.Should().Contain("–");
        cut.Find("[data-likert-results] thead").TextContent.Should().Contain("Disagree").And.Contain("Average");

        cut.Find("[data-result-key='hours']").TextContent.Should().Contain("Slider").And.Contain("25").And.Contain("40");

        cut.FindAll("[data-add-filter] optgroup").Select(g => g.GetAttribute("label")).Should().Equal(["Would you recommend us?"],
            "an NPS question can filter the results, but a grid or a slider can't");
    }

    [Fact]
    public async Task Comparisons_ShowEachGroupsNps_AndRowAverages()
    {
        await using var ctx = CreateContext();
        var id = Guid.NewGuid();
        QuestionResult Nps(int score) => new()
        {
            Key = "recommend",
            Label = "Would you recommend us?",
            Type = "nps",
            AnsweredCount = 2,
            Options = Enumerable.Range(0, 11).Select(n => new OptionCount { Value = $"{n}", Label = $"{n}" }).ToList(),
            Nps = new NpsSummary { Score = score }
        };
        QuestionResult Grid(decimal? average) => new()
        {
            Key = "services",
            Label = "How much do you agree?",
            Type = "likert",
            AnsweredCount = 2,
            Rows = [new RowResult { Value = "library", Label = "The library is great", AnsweredCount = 2, Average = average, Options = [new OptionCount { Value = "1", Label = "Agree" }] }]
        };
        _service.Results = new SurveyResults
        {
            SurveyId = id,
            Title = "Campus survey",
            TotalResponses = 4,
            MatchingResponses = 4,
            Questions = [Nps(0), Grid(3)],
            Comparison = new ResultComparison
            {
                Key = "is_student",
                Label = "Student",
                Groups =
                [
                    new ResultGroup { Value = "true", Label = "Yes", Responses = 2, Questions = [Nps(-50), Grid(2.5m)] },
                    new ResultGroup { Value = "false", Label = "No", Responses = 2, Questions = [Nps(50), Grid(null)] }
                ]
            }
        };

        var cut = ctx.Render<AdminSurveyResults>(p => p.Add(x => x.Id, id));

        cut.WaitForAssertion(() => cut.FindAll("[data-comparison]").Should().HaveCount(2));
        var nps = cut.Find("[data-result-key='recommend'] [data-comparison]").TextContent;
        nps.Should().Contain("-50").And.Contain("+50");
        var grid = cut.Find("[data-result-key='services'] [data-comparison]").TextContent;
        grid.Should().Contain("Average: The library is great").And.Contain("2.5").And.Contain("–");
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
