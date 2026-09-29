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
    public async Task RendersCountsNumbersAndCsvLink()
    {
        await using var ctx = CreateContext();
        var id = Guid.NewGuid();
        _service.Results = new SurveyResults
        {
            SurveyId = id,
            Title = "Campus survey",
            TotalResponses = 4,
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
        cut.Find("a[href$='/responses/export']").Should().NotBeNull();
    }

    [Fact]
    public async Task MissingSurvey_ShowsNotFound()
    {
        await using var ctx = CreateContext();
        var cut = ctx.Render<AdminSurveyResults>(p => p.Add(x => x.Id, Guid.NewGuid()));

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Survey not found"));
    }
}
