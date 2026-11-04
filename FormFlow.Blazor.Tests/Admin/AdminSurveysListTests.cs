using System.Net;
using System.Text;
using System.Text.Json;
using Bunit;
using FluentAssertions;
using FormFlow.Blazor.Components.Pages.Admin;
using FormFlow.Blazor.Services;
using FormFlow.Blazor.Tests.Respond;
using FormFlow.Data.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
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

        /// <summary>The links in the survey's row, by text, so they open like any link (also in a new tab).</summary>
        private static Dictionary<string, string?> RowLinks(IRenderedComponent<AdminSurveysList> cut, int row = 0) =>
            cut.FindAll("tbody tr")[row].QuerySelectorAll("a").ToDictionary(a => a.TextContent.Trim(), a => a.GetAttribute("href"));

        [Fact]
        public async Task SurveyList_LinksToEachSurveysPages()
        {
            await using var ctx = CreateContext();
            var survey = Survey("Survey A", 0);
            _service.Surveys.Add(survey);

            var cut = ctx.Render<AdminSurveysList>();
            cut.WaitForAssertion(() => Assert.Contains("Survey A", cut.Markup));

            RowLinks(cut).Should().BeEquivalentTo(new Dictionary<string, string?>
            {
                ["Edit"] = $"/admin/surveys/{survey.Id}/edit",
                ["Share"] = $"/admin/surveys/{survey.Id}/share",
                ["Preview"] = $"/admin/surveys/{survey.Id}/preview",
                ["Results"] = $"/admin/surveys/{survey.Id}/results",
            });
        }

        [Fact]
        public async Task SurveyList_SaysSoWhenTheSurveysCannotBeLoaded()
        {
            await using var ctx = CreateContext();
            _service.Unreachable = true;

            var cut = ctx.Render<AdminSurveysList>();

            cut.WaitForAssertion(() => Assert.Contains("Could not load surveys.", cut.Markup));
            cut.Markup.Should().NotContain("No surveys found");
        }

        [Fact]
        public async Task SurveyList_ProfessorView_ShowsOnlyTheirOwnSurveys()
        {
            var me = Guid.NewGuid();
            await using var ctx = CreateContext();
            var mine = Survey("Mine");
            mine.OwnerId = me;
            _service.Surveys.AddRange([mine, Survey("Someone else's")]);

            // An administrator previewing the professor view gets every survey from the API; the page keeps their own.
            var cut = ctx.Render<AdminSurveysList>(p => p.AddCascadingValue(new AdminAccess("professor", me)));
            cut.WaitForAssertion(() => Assert.Contains("Mine", cut.Markup));

            cut.Markup.Should().NotContain("Someone else");
            cut.Markup.Should().Contain("Create Survey");
            cut.Markup.Should().NotContain("Created by", "only administrators see who made each survey");
            RowLinks(cut).Keys.Should().Contain(["Edit", "Results", "Share"]);
        }

        [Fact]
        public async Task SurveyList_SwitchingToTheProfessorView_UpdatesTheList()
        {
            var me = Guid.NewGuid();
            await using var ctx = CreateContext();
            var mine = Survey("Mine");
            mine.OwnerId = me;
            _service.Surveys.AddRange([mine, Survey("Demo")]);

            var cut = ctx.Render<CascadingValue<AdminAccess>>(p => p
                .Add(c => c.Value, new AdminAccess("admin", me))
                .AddChildContent<AdminSurveysList>());
            cut.WaitForAssertion(() => Assert.Contains("Demo", cut.Markup));

            cut.Render(p => p
                .Add(c => c.Value, new AdminAccess("professor", me))
                .AddChildContent<AdminSurveysList>());

            cut.Markup.Should().Contain("Mine");
            cut.Markup.Should().NotContain("Demo");
        }

        [Fact]
        public async Task SurveyList_AdminView_ShowsWhoCreatedEachSurvey()
        {
            await using var ctx = CreateContext();
            var theirs = Survey("Lab feedback");
            theirs.OwnerId = Guid.NewGuid();
            theirs.OwnerName = "professor";
            _service.Surveys.AddRange([theirs, Survey("Demo")]);

            var cut = ctx.Render<AdminSurveysList>();
            cut.WaitForAssertion(() => Assert.Contains("Lab feedback", cut.Markup));

            cut.Markup.Should().Contain("Created by");
            cut.FindAll("tbody tr")[0].TextContent.Should().Contain("professor");
            cut.FindAll("tbody tr")[1].TextContent.Should().Contain("Administrators");
            cut.FindAll("a").Where(a => a.TextContent.Trim() == "Edit").Should().HaveCount(2);
        }

        [Fact]
        public async Task SurveyList_ShowsEachSurveysSharingStatus()
        {
            await using var ctx = CreateContext();
            var draft = Survey("Draft one");
            draft.Status = SurveyStatuses.Draft;
            var linkOnly = Survey("Link one");
            linkOnly.Listed = false;
            var closed = Survey("Closed one");
            closed.ClosesAt = DateTime.UtcNow.AddDays(-1);
            _service.Surveys.AddRange([draft, linkOnly, closed, Survey("Listed one")]);

            var cut = ctx.Render<AdminSurveysList>();
            cut.WaitForAssertion(() => Assert.Contains("Listed one", cut.Markup));

            cut.FindAll("[data-survey-status]").Select(c => c.GetAttribute("data-survey-status"))
                .Should().Equal("Draft", "Published, link only", "Closed", "Published");
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
        public async Task SurveyList_Duplicate_OpensTheNewDraftForEditing()
        {
            await using var ctx = CreateContext();
            var survey = Survey("Survey A", 2);
            _service.Surveys.Add(survey);
            var snackbars = ctx.Render<MudSnackbarProvider>();
            var cut = ctx.Render<AdminSurveysList>();
            cut.WaitForAssertion(() => Assert.Contains("Survey A", cut.Markup));

            cut.Find("button[aria-label='Duplicate Survey A']").Click();

            cut.WaitForAssertion(() => _service.Duplicated.Should().Equal(survey.Id));
            var copy = _service.Surveys.Single(s => s.Id != survey.Id);
            copy.Title.Should().Be("Copy of Survey A");
            ctx.Services.GetRequiredService<NavigationManager>().Uri.Should().EndWith($"/admin/surveys/{copy.Id}/edit");
            snackbars.WaitForAssertion(() => snackbars.Markup.Should().Contain("Created \"Copy of Survey A\" as a draft."));
        }

        [Fact]
        public async Task SurveyList_ProfessorsCanDuplicateTheirSurveys()
        {
            var me = Guid.NewGuid();
            await using var ctx = CreateContext();
            var mine = Survey("Mine");
            mine.OwnerId = me;
            _service.Surveys.Add(mine);

            var cut = ctx.Render<AdminSurveysList>(p => p.AddCascadingValue(new AdminAccess("professor", me)));
            cut.WaitForAssertion(() => Assert.Contains("Mine", cut.Markup));

            cut.FindAll("button[aria-label='Duplicate Mine']").Should().ContainSingle();
        }

        [Fact]
        public async Task SurveyList_FailedDuplicate_StaysAndShowsWhy()
        {
            await using var ctx = CreateContext();
            _service.Surveys.Add(Survey("Survey A"));
            _service.NextCopyError = "Could not reach the server. Please try again.";
            var snackbars = ctx.Render<MudSnackbarProvider>();
            var cut = ctx.Render<AdminSurveysList>();
            cut.WaitForAssertion(() => Assert.Contains("Survey A", cut.Markup));

            cut.Find("button[aria-label='Duplicate Survey A']").Click();

            snackbars.WaitForAssertion(() => snackbars.Markup.Should().Contain("The survey could not be duplicated: Could not reach the server."));
            ctx.Services.GetRequiredService<NavigationManager>().Uri.Should().NotContain("/edit");
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
