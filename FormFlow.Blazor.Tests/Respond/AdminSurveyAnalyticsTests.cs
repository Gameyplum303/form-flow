using Bunit;
using FluentAssertions;
using FormFlow.Blazor.Components.Pages.Admin;
using FormFlow.Blazor.Services;
using FormFlow.Data.Models;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;

namespace FormFlow.Blazor.Tests.Respond;

/// <summary>Filters, dates, the timeline and group comparisons on the results page.</summary>
public class AdminSurveyAnalyticsTests
{
    private readonly FakeSurveyService _service = new();
    private readonly Guid _id = Guid.NewGuid();

    private BunitContext CreateContext(int timezoneOffset = 0)
    {
        var ctx = new BunitContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        ctx.JSInterop.Setup<int>("formFlow.timezoneOffset").SetResult(timezoneOffset);
        ctx.Services.AddMudServices();
        ctx.Services.AddSingleton<ISurveyService>(_service);
        _service.ResultsFor = Results;
        return ctx;
    }

    private static QuestionResult Student(int yes, int no) => new()
    {
        Key = "is_student",
        Label = "Are you a student?",
        Type = "yes_no",
        AnsweredCount = yes + no,
        Options = [new() { Value = "true", Label = "Yes", Count = yes }, new() { Value = "false", Label = "No", Count = no }]
    };

    private static QuestionResult Age(decimal average, int answered) => new()
    {
        Key = "age",
        Label = "Age",
        Type = "number",
        AnsweredCount = answered,
        Numbers = new NumberSummary { Min = 18, Max = 40, Average = average }
    };

    private static QuestionResult Comments() => new()
    {
        Key = "comments",
        Label = "Anything else?",
        Type = "long_text",
        AnsweredCount = 1,
        RecentAnswers = ["More study rooms."]
    };

    /// <summary>Four responses: three students averaging 21, one non-student aged 40.</summary>
    private SurveyResults Results(ResultsQuery? query)
    {
        var studentsOnly = query?.Filters.Contains(new AnswerFilter("is_student", "true")) == true;
        var results = new SurveyResults
        {
            SurveyId = _id,
            Title = "Campus survey",
            TotalResponses = 4,
            MatchingResponses = studentsOnly ? 3 : 4,
            Questions = studentsOnly ? [Student(3, 0), Age(21, 3), Comments()] : [Student(3, 1), Age(25.75m, 4), Comments()],
            Timeline =
            [
                new() { Start = "2026-09-01", Count = 2 },
                new() { Start = "2026-09-02", Count = 0 },
                new() { Start = "2026-09-03", Count = studentsOnly ? 1 : 2 },
            ]
        };
        if (query?.CompareBy == "is_student")
        {
            results.Comparison = new ResultComparison
            {
                Key = "is_student",
                Label = "Are you a student?",
                Groups =
                [
                    new() { Value = "true", Label = "Yes", Responses = 3, Questions = [Student(3, 0), Age(21, 3), Comments()] },
                    new() { Value = "false", Label = "No", Responses = 1, Questions = [Student(0, 1), Age(40, 1), Comments()] },
                ]
            };
        }
        return results;
    }

    private IRenderedComponent<AdminSurveyResults> Render(BunitContext ctx)
    {
        var cut = ctx.Render<AdminSurveyResults>(p => p.Add(x => x.Id, _id));
        cut.WaitForAssertion(() => cut.Find("[data-analysis]"));
        return cut;
    }

    [Fact]
    public async Task AddingAFilter_AsksForMatchingResponses_AndShowsAChip()
    {
        await using var ctx = CreateContext();
        var cut = Render(ctx);
        cut.FindAll("[data-matching]").Should().BeEmpty("nothing is filtered yet");

        cut.Find("[data-add-filter]").Change("is_student:true");

        cut.WaitForAssertion(() => cut.Find("[data-matching]").TextContent.Should().Contain("3 of 4 responses match"));
        _service.ResultsQueries.Last()!.Filters.Should().Equal(new AnswerFilter("is_student", "true"));
        cut.Find("[data-filter-chip]").TextContent.Should().Contain("Are you a student?: Yes");
        cut.Find("[data-result-key='age']").TextContent.Should().Contain("answered by 3 of 3").And.Contain("21");
        cut.FindAll("[data-add-filter] option").Select(o => o.TextContent).Should().NotContain("Yes", "a filter can't be added twice");
    }

    [Fact]
    public async Task ClearAll_RemovesEveryFilter()
    {
        await using var ctx = CreateContext();
        var cut = Render(ctx);
        cut.Find("[data-add-filter]").Change("is_student:true");
        cut.WaitForAssertion(() => cut.Find("[data-filter-chip]"));

        cut.Find("[data-clear-analysis]").Click();

        cut.WaitForAssertion(() => cut.FindAll("[data-filter-chip]").Should().BeEmpty());
        _service.ResultsQueries.Last()!.Filters.Should().BeEmpty();
        cut.Find("[data-result-key='age']").TextContent.Should().Contain("answered by 4 of 4");
    }

    [Fact]
    public async Task CompareBy_ShowsEachGroupSideBySide()
    {
        await using var ctx = CreateContext();
        var cut = Render(ctx);

        cut.Find("[data-compare-by]").Change("is_student");

        cut.WaitForAssertion(() => cut.Find("[data-result-key='age'] [data-comparison]"));
        var table = cut.Find("[data-result-key='age'] [data-comparison]").TextContent;
        table.Should().Contain("Yes (3)").And.Contain("No (1)").And.Contain("21").And.Contain("40").And.Contain("1 of 1");
        cut.FindAll("[data-result-key='is_student'] [data-comparison]").Should().BeEmpty("a question isn't compared with itself");
        cut.FindAll("[data-result-key='comments'] [data-comparison]").Should().BeEmpty("text answers aren't compared");
        _service.ResultsQueries.Last()!.CompareBy.Should().Be("is_student");
    }

    [Fact]
    public async Task ComparisonPercentages_AreOfThoseInTheGroupWhoAnswered()
    {
        await using var ctx = CreateContext();
        _service.ResultsFor = query =>
        {
            var results = Results(query);
            results.Questions = [Student(3, 1), new QuestionResult
            {
                Key = "campus", Label = "Campus", Type = "radio", AnsweredCount = 4,
                Options = [new() { Value = "north", Label = "North", Count = 3 }, new() { Value = "south", Label = "South", Count = 1 }]
            }];
            if (results.Comparison is { } comparison)
            {
                comparison.Groups[0].Questions = [Student(3, 0), new QuestionResult
                {
                    Key = "campus", AnsweredCount = 3, Options = [new() { Count = 2 }, new() { Count = 1 }]
                }];
                comparison.Groups[1].Questions = [Student(0, 1), new QuestionResult
                {
                    Key = "campus", AnsweredCount = 0, Options = [new() { Count = 0 }, new() { Count = 0 }]
                }];
            }
            return results;
        };
        var cut = Render(ctx);

        cut.Find("[data-compare-by]").Change("is_student");

        cut.WaitForAssertion(() => cut.Find("[data-result-key='campus'] [data-comparison]"));
        var rows = cut.FindAll("[data-result-key='campus'] [data-comparison] tbody tr").Select(r => r.TextContent).ToList();
        rows[0].Should().Contain("North").And.Contain("2 (67%)").And.Contain("–");
        rows[1].Should().Contain("South").And.Contain("1 (33%)");
    }

    [Fact]
    public async Task Timeline_DrawsABarPerDay_ScaledToTheBusiestDay()
    {
        await using var ctx = CreateContext();
        var cut = Render(ctx);

        var points = cut.FindAll("[data-timeline-point]");
        points.Select(p => p.GetAttribute("data-count")).Should().Equal("2", "0", "2");
        points[0].GetAttribute("title").Should().Be("Sep 1, 2026: 2 responses");
        points[0].QuerySelector(".timeline-bar")!.GetAttribute("style").Should().Contain("height: 100%");
        points[1].QuerySelector(".timeline-bar")!.GetAttribute("style").Should().Contain("height: 0%");
        cut.Find("[data-timeline] .timeline-axis").TextContent.Should().Contain("Sep 1, 2026").And.Contain("Sep 3, 2026");
        cut.Find("[data-timeline] .timeline").GetAttribute("aria-label").Should().Contain("at most 2 in one day");
    }

    [Fact]
    public async Task Dates_AreLocalDays_SentToTheApiInUtc()
    {
        // Five hours behind UTC, as getTimezoneOffset reports US Eastern in winter.
        await using var ctx = CreateContext(timezoneOffset: 300);
        var cut = Render(ctx);
        cut.WaitForAssertion(() => _service.ResultsQueries.Last()!.UtcOffsetMinutes.Should().Be(-300));

        cut.Find("[data-from]").Change("2026-09-01");
        cut.Find("[data-to]").Change("2026-09-02");

        cut.WaitForAssertion(() => _service.ResultsQueries.Last()!.To.Should().NotBeNull());
        var query = _service.ResultsQueries.Last()!;
        query.From.Should().Be(new DateTime(2026, 9, 1, 5, 0, 0, DateTimeKind.Utc));
        query.To.Should().Be(new DateTime(2026, 9, 3, 5, 0, 0, DateTimeKind.Utc), "the whole of the last day counts");
        cut.Find("[data-matching]").TextContent.Should().Contain("4 of 4");
    }

    [Fact]
    public async Task NoMatches_SaysSo_AndDisablesTheDownload()
    {
        await using var ctx = CreateContext();
        _service.ResultsFor = query =>
        {
            var results = Results(query);
            if (query?.Filters.Count > 0)
            {
                results.MatchingResponses = 0;
            }
            return results;
        };
        var cut = Render(ctx);

        cut.Find("[data-add-filter]").Change("is_student:false");

        cut.WaitForAssertion(() => cut.Find("[data-no-matches]"));
        cut.FindAll("button").Single(b => b.TextContent.Contains("Download CSV")).HasAttribute("disabled").Should().BeTrue();
    }

    [Fact]
    public async Task DownloadCsv_SendsTheSameFilters()
    {
        await using var ctx = CreateContext();
        _service.Export = new CsvExport("campus-survey-responses.csv", [1]);
        var cut = Render(ctx);
        cut.Find("[data-add-filter]").Change("is_student:true");
        cut.WaitForAssertion(() => cut.Find("[data-filter-chip]"));

        cut.FindAll("button").Single(b => b.TextContent.Contains("Download CSV")).Click();

        cut.WaitForAssertion(() => _service.LastExportQuery!.Filters.Should().Equal(new AnswerFilter("is_student", "true")));
    }

    [Fact]
    public async Task AFailedRefresh_KeepsTheLastResults_AndSaysSo()
    {
        await using var ctx = CreateContext();
        var cut = Render(ctx);
        ctx.Render<MudSnackbarProvider>();
        _service.ResultsFor = _ => null;

        cut.Find("[data-compare-by]").Change("is_student");

        cut.WaitForAssertion(() => ctx.Services.GetRequiredService<ISnackbar>().ShownSnackbars
            .Should().ContainSingle(s => s.Message == "The results could not be updated. Try again."));
        cut.Find("[data-result-key='age']").TextContent.Should().Contain("25.75");
    }
}
