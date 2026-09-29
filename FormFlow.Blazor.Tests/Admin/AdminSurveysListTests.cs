using System.Net;
using System.Text;
using System.Text.Json;
using Bunit;
using FluentAssertions;
using FormFlow.Blazor.Components.Pages.Admin;
using FormFlow.Blazor.Services;
using FormFlow.Blazor.Tests.Respond;
using FormFlow.Data.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using MudBlazor.Services;


namespace FormFlow.Blazor.Tests.Admin
{
    public class AdminSurveysListTests
    {
        private readonly FakeSurveyService _service = new();

        private BunitContext CreateContext()
        {
            var ctx = new BunitContext();
            ctx.JSInterop.Mode = JSRuntimeMode.Loose;
            ctx.Services.AddMudServices();
            ctx.Services.AddSingleton<ISurveyService>(_service);
            ctx.Render<MudPopoverProvider>();

            return ctx;
        }

        private static SurveyDefinition Survey(string title, int questions = 1) => new()
        {
            Id = Guid.NewGuid(),
            Title = title,
            Description = $"About {title}",
            QuestionIds = Enumerable.Range(0, questions).Select(_ => Guid.NewGuid()).ToList(),
            CreatedAt = DateTime.UtcNow
        };

        [Fact]
        public async Task SurveyList_LoadsAndDisplaysSurveys()
        {
            await using var ctx = CreateContext();
            _service.Surveys.AddRange([Survey("Survey A"), Survey("Survey B", 2)]);

            var cut = ctx.Render<AdminSurveysList>();

            cut.WaitForAssertion(() => Assert.Contains("Survey A", cut.Markup));
            Assert.Contains("Survey B", cut.Markup);
        }

        [Fact]
        public async Task SurveyList_ShowsEmptyMessage_WhenNoSurveys()
        {
            await using var ctx = CreateContext();

            var cut = ctx.Render<AdminSurveysList>();

            cut.WaitForAssertion(() => Assert.Contains("No surveys found", cut.Markup));
        }

        [Fact]
        public async Task SurveyList_PreviewButton_NavigatesToPreviewPage()
        {
            await using var ctx = CreateContext();
            var survey = Survey("Survey A", 0);
            _service.Surveys.Add(survey);
            var nav = ctx.Services.GetRequiredService<NavigationManager>();

            var cut = ctx.Render<AdminSurveysList>();
            cut.WaitForAssertion(() => Assert.Contains("Survey A", cut.Markup));
            cut.FindAll("button").First(b => b.TextContent.Contains("Preview", StringComparison.OrdinalIgnoreCase)).Click();

            Assert.Equal($"admin/surveys/{survey.Id}/preview", nav.Uri.Replace(nav.BaseUri, ""));
        }

        [Fact]
        public async Task SurveyList_ViewOnlyAccount_HasNoCreateEditOrDelete()
        {
            await using var ctx = CreateContext();
            _service.Surveys.Add(Survey("Survey A"));

            var cut = ctx.Render<AdminSurveysList>(p => p.AddCascadingValue(new AdminAccess(false)));
            cut.WaitForAssertion(() => Assert.Contains("Survey A", cut.Markup));

            cut.Markup.Should().NotContain("Create Survey");
            cut.FindAll("button").Should().NotContain(b => b.TextContent.Trim() == "Edit");
            cut.FindAll("button[aria-label='Delete Survey A']").Should().BeEmpty();
            cut.FindAll("button").Should().Contain(b => b.TextContent.Trim() == "Results");
        }

        [Fact]
        public async Task SurveyList_ConfirmedDelete_RemovesTheSurvey()
        {
            await using var ctx = CreateContext();
            var survey = Survey("Survey A");
            _service.Surveys.Add(survey);
            var dialogs = ctx.Render<MudDialogProvider>();
            var cut = ctx.Render<AdminSurveysList>();
            cut.WaitForAssertion(() => Assert.Contains("Survey A", cut.Markup));

            cut.Find("button[aria-label='Delete Survey A']").Click();
            dialogs.WaitForAssertion(() => dialogs.Markup.Should().Contain("This cannot be undone."));
            dialogs.FindAll("button").First(b => b.TextContent.Trim() == "Delete").Click();

            cut.WaitForAssertion(() => cut.Markup.Should().NotContain("Survey A"));
            _service.Deleted.Should().Equal(survey.Id);
        }

        [Fact]
        public async Task SurveyList_RefusedDelete_KeepsTheSurveyAndShowsWhy()
        {
            await using var ctx = CreateContext();
            _service.Surveys.Add(Survey("Survey A"));
            _service.NextDeleteResult = (false, "Request failed (401)");
            var dialogs = ctx.Render<MudDialogProvider>();
            var snackbars = ctx.Render<MudSnackbarProvider>();
            var cut = ctx.Render<AdminSurveysList>();
            cut.WaitForAssertion(() => Assert.Contains("Survey A", cut.Markup));

            cut.Find("button[aria-label='Delete Survey A']").Click();
            dialogs.WaitForAssertion(() => dialogs.FindAll("button").Should().Contain(b => b.TextContent.Trim() == "Delete"));
            dialogs.FindAll("button").First(b => b.TextContent.Trim() == "Delete").Click();

            snackbars.WaitForAssertion(() => snackbars.Markup.Should().Contain("The survey could not be deleted: Request failed (401)"));
            cut.Markup.Should().Contain("Survey A");
        }
    }

    // ===================================================================
    // Mock HTTP Handler — MUST inherit from DelegatingHandler
    // MUST have a public parameterless constructor
    // ===================================================================
    public class MockHttpMessageHandler : DelegatingHandler
    {
        private readonly Dictionary<string, HttpResponseMessage> _responses = new();

        // REQUIRED: parameterless constructor
        public MockHttpMessageHandler() : base()
        {
        }

        public void SetJsonResponse(string urlContains, object responseObject)
        {
            var json = JsonSerializer.Serialize(responseObject);
            var message = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };

            _responses[urlContains] = message;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            foreach (var entry in _responses)
            {
                if (request.RequestUri!.ToString().Contains(entry.Key))
                    return Task.FromResult(entry.Value);
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }
}
