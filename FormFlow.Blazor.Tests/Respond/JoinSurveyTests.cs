using Bunit;
using FluentAssertions;
using FormFlow.Blazor.Components.Pages.Respond;
using FormFlow.Blazor.Services;
using FormFlow.Data.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;

namespace FormFlow.Blazor.Tests.Respond;

public class JoinSurveyTests
{
    private readonly FakeSurveyService _service = new();

    public JoinSurveyTests() => _service.Surveys.Add(new SurveyDefinition
    {
        Id = Guid.NewGuid(),
        Title = "Week 3 check-in",
        Description = "How is the course going?",
        QuestionIds = [],
        CreatedAt = DateTime.UtcNow,
        Status = SurveyStatuses.Published,
        ShareCode = "k7m2p9qa",
    });

    private BunitContext CreateContext()
    {
        var ctx = new BunitContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        ctx.Services.AddMudServices();
        ctx.Services.AddSingleton<ISurveyService>(_service);
        ctx.Render<MudPopoverProvider>();
        return ctx;
    }

    private static async Task JoinAsync(IRenderedComponent<JoinSurvey> cut, string code)
    {
        await cut.InvokeAsync(() => cut.FindComponent<MudTextField<string>>().Instance.ValueChanged.InvokeAsync(code));
        await cut.InvokeAsync(() => cut.Find("form").Submit());
    }

    [Theory]
    [InlineData("k7m2p9qa")]
    [InlineData("K7M2 P9QA")]
    [InlineData(" k7m2-p9qa ")]
    [InlineData("https://formflow.example/s/K7M2P9QA")]
    public async Task ACode_OpensItsSurvey_HoweverItIsTyped(string typed)
    {
        await using var ctx = CreateContext();
        var nav = ctx.Services.GetRequiredService<NavigationManager>();
        var cut = ctx.Render<JoinSurvey>();

        await JoinAsync(cut, typed);

        cut.WaitForAssertion(() => nav.Uri.Should().Be("http://localhost/s/k7m2p9qa"));
    }

    [Theory]
    [InlineData("", "Enter the survey code.")]
    [InlineData("k7m2", "That doesn't look like a survey code.")]
    [InlineData("k7m2p9q0", "That doesn't look like a survey code.")]
    [InlineData("k7m2p9qb", "No open survey has that code.")]
    public async Task AWrongCode_SaysWhatIsWrong_AndStaysPut(string typed, string message)
    {
        await using var ctx = CreateContext();
        var nav = ctx.Services.GetRequiredService<NavigationManager>();
        var cut = ctx.Render<JoinSurvey>();

        await JoinAsync(cut, typed);

        cut.WaitForAssertion(() => cut.Markup.Should().Contain(message));
        nav.Uri.Should().Be("http://localhost/");
    }

    [Fact]
    public async Task TheCodeBox_IsEasyToTypeOnAPhone()
    {
        await using var ctx = CreateContext();
        var cut = ctx.Render<JoinSurvey>();

        var input = cut.Find("input");
        input.GetAttribute("autocomplete").Should().Be("off");
        input.GetAttribute("autocapitalize").Should().Be("characters");
        input.GetAttribute("autocorrect").Should().Be("off");
        input.GetAttribute("spellcheck").Should().Be("false");
    }
}
