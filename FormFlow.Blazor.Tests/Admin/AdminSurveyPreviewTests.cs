using Bunit;
using FluentAssertions;
using FormFlow.Blazor.Components;
using FormFlow.Blazor.Components.Pages.Admin;
using FormFlow.Blazor.Services;
using FormFlow.Blazor.Tests.Respond;
using FormFlow.Data.Models;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using MudBlazor.Services;

namespace FormFlow.Blazor.Tests.Admin
{
    public class AdminSurveyPreviewTests : BunitContext
    {
        private readonly FakeSurveyService _surveys = new();
        private readonly Guid _surveyId = Guid.NewGuid();

        private readonly QuestionDefinition _q1 = new()
        {
            Id = Guid.NewGuid(),
            Label = "How satisfied are you?",
            Key = "satisfaction",
            Type = "rating"
        };

        private readonly QuestionDefinition _q2 = new()
        {
            Id = Guid.NewGuid(),
            Label = "Any comments?",
            Key = "comments",
            Type = "text"
        };

        public AdminSurveyPreviewTests()
        {
            Services.AddMudServices();
            JSInterop.Mode = JSRuntimeMode.Loose;
        }

        private SurveyDefinition AddSurvey()
        {
            var survey = new SurveyDefinition
            {
                Id = _surveyId,
                Title = "Customer Satisfaction Survey",
                Description = "A test survey",
                QuestionIds = [_q1.Id, _q2.Id],
                CreatedAt = DateTime.UtcNow
            };
            _surveys.Surveys.Add(survey);
            _surveys.Questions[_surveyId] = [_q1, _q2];
            Services.AddSingleton<ISurveyService>(_surveys);
            return survey;
        }

        private IRenderedComponent<AdminSurveyPreview> RenderPreview() =>
            Render<AdminSurveyPreview>(parameters => parameters.Add(p => p.Id, _surveyId));

        [Fact]
        public void ShowsLoadingStateBeforeDataLoads()
        {
            var service = new Mock<ISurveyService>();
            service.Setup(s => s.GetSurveyAsync(_surveyId)).Returns(new TaskCompletionSource<SurveyDefinition?>().Task);
            Services.AddSingleton(service.Object);

            var cut = RenderPreview();

            cut.Markup.Should().Contain("mud-progress-circular");
        }

        [Fact]
        public void LoadsSurveyAndRendersTitle()
        {
            var survey = AddSurvey();

            var cut = RenderPreview();

            cut.WaitForAssertion(() => cut.Markup.Should().Contain(survey.Title));
        }

        [Fact]
        public void RendersAllQuestionsInOrder()
        {
            AddSurvey();

            var cut = RenderPreview();

            cut.WaitForState(() => cut.FindComponents<QuestionRenderer>().Count == 2);
            var renderers = cut.FindComponents<QuestionRenderer>();
            renderers[0].Instance.Question.Should().BeEquivalentTo(_q1);
            renderers[1].Instance.Question.Should().BeEquivalentTo(_q2);
        }

        [Fact]
        public void ShowsPagedSurveysOnePageAtATime_WithoutASubmitButton()
        {
            var survey = AddSurvey();
            survey.PageBreaks = [_q2.Id];

            var cut = RenderPreview();

            cut.WaitForAssertion(() => cut.Find("[data-page-label]").TextContent.Trim().Should().Be("Page 1 of 2"));
            cut.FindComponents<QuestionRenderer>().Should().ContainSingle().Which.Instance.Question.Should().BeEquivalentTo(_q1);

            // The preview checks answers like the real page: the rating is required.
            cut.FindAll("button").Single(b => b.TextContent.Trim() == "Next").Click();
            cut.WaitForAssertion(() => cut.Markup.Should().Contain("This question is required."));
            cut.Find("[data-page-label]").TextContent.Trim().Should().Be("Page 1 of 2");

            _q1.Required = false;
            cut.FindAll("button").Single(b => b.TextContent.Trim() == "Next").Click();
            cut.WaitForAssertion(() => cut.Find("[data-page-label]").TextContent.Trim().Should().Be("Page 2 of 2"));
            cut.FindComponents<QuestionRenderer>().Should().ContainSingle().Which.Instance.Question.Should().BeEquivalentTo(_q2);
            cut.FindAll("button").Select(b => b.TextContent.Trim()).Should().Contain("Back").And.NotContain("Submit").And.NotContain("Next");
        }

        [Fact]
        public void HandlesMissingSurveyGracefully()
        {
            Services.AddSingleton<ISurveyService>(_surveys);

            var cut = RenderPreview();

            cut.WaitForAssertion(() => cut.Markup.Should().Contain("Survey not found"));
        }

        [Fact]
        public void SaysSoWhenTheQuestionsCannotBeLoaded()
        {
            var survey = AddSurvey();
            _surveys.QuestionsUnreachable = true;

            var cut = RenderPreview();

            cut.WaitForAssertion(() => cut.Markup.Should().Contain("Could not load the survey"));
            cut.Markup.Should().Contain(survey.Title);
            cut.FindComponents<QuestionRenderer>().Should().BeEmpty();
        }
    }
}
