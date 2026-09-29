using Bunit;
using FluentAssertions;
using FormFlow.Blazor.Components.Pages.Respond;
using FormFlow.Blazor.Services;
using FormFlow.Data.Models;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;

namespace FormFlow.Blazor.Tests.Respond;

public class SurveyListTests
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
    public async Task Lists_each_survey_with_a_link_to_take_it()
    {
        var survey = new SurveyDefinition
        {
            Id = Guid.NewGuid(),
            Title = "Campus life",
            Description = "About campus",
            QuestionIds = [Guid.NewGuid(), Guid.NewGuid()],
            CreatedAt = DateTime.UtcNow
        };
        _service.Surveys.Add(survey);
        await using var ctx = CreateContext();

        var cut = ctx.Render<SurveyList>();

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Campus life"));
        cut.Markup.Should().Contain("2 questions");
        cut.Find($"a[href='/surveys/{survey.Id}']").TextContent.Trim().Should().Be("Take survey");
    }

    [Fact]
    public async Task Shows_a_message_when_there_are_no_surveys()
    {
        await using var ctx = CreateContext();

        var cut = ctx.Render<SurveyList>();

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("There are no surveys to take yet."));
    }

    [Fact]
    public async Task Says_so_when_the_surveys_cannot_be_loaded()
    {
        _service.Unreachable = true;
        await using var ctx = CreateContext();

        var cut = ctx.Render<SurveyList>();

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Could not load the surveys."));
        cut.Markup.Should().NotContain("There are no surveys to take yet.");
    }
}
