using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using FormFlow.Backend.Repositories;
using FormFlow.Data.Models;
using FormFlow.Data.Services;
using Microsoft.Extensions.DependencyInjection;

namespace FormFlow.Backend.Tests.Endpoints
{
    /// <summary>
    /// Templates are seeded, read-only surveys that builders copy into a new draft. They never show on
    /// the survey lists and can't be opened, answered, shared or changed themselves.
    /// </summary>
    public class TemplateEndpointTests : IDisposable
    {
        private readonly InMemoryApiFactory _factory = new();
        private readonly HttpClient _professor;
        private readonly HttpClient _admin;
        private readonly HttpClient _anonymous;

        private static readonly string[] TemplateTitles =
            ["Course evaluation", "Customer satisfaction", "Event feedback", "Research study intake"];

        public TemplateEndpointTests()
        {
            _professor = _factory.CreateClient().AsProfessor();
            _admin = _factory.CreateClient().AsAdmin();
            _anonymous = _factory.CreateClient();
        }

        public void Dispose() => _factory.Dispose();

        private async Task<List<SurveyTemplate>> TemplatesAsync(HttpClient? client = null) =>
            (await (client ?? _professor).GetFromJsonAsync<List<SurveyTemplate>>("/api/templates"))!;

        private async Task<SurveyTemplate> TemplateAsync(string title) => (await TemplatesAsync()).Single(t => t.Title == title);

        private async Task<SurveyDefinition> UseAsync(HttpClient client, Guid templateId)
        {
            var response = await client.PostAsync($"/api/templates/{templateId}/use", null);
            response.StatusCode.Should().Be(HttpStatusCode.Created);
            return (await response.Content.ReadFromJsonAsync<SurveyDefinition>())!;
        }

        [Fact]
        public async Task Templates_AreListedForBuilders_WithTheirQuestionCounts()
        {
            var templates = await TemplatesAsync();

            templates.Select(t => t.Title).Should().Equal(TemplateTitles);
            templates.Should().OnlyContain(t => t.QuestionCount > 0 && !string.IsNullOrWhiteSpace(t.Description));
            var course = templates.Single(t => t.Title == "Course evaluation");
            course.QuestionCount.Should().Be(10);
            course.PageCount.Should().Be(3);
            (await TemplatesAsync(_admin)).Should().HaveCount(4);
        }

        [Fact]
        public async Task Templates_NeedABuilder()
        {
            (await _anonymous.GetAsync("/api/templates")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            var template = await TemplateAsync("Event feedback");
            (await _anonymous.PostAsync($"/api/templates/{template.Id}/use", null)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task Templates_AreNotOnThePublicOrManagedLists()
        {
            var ids = (await TemplatesAsync()).Select(t => t.Id).ToList();

            var publicList = await _anonymous.GetFromJsonAsync<List<SurveyDefinition>>("/api/surveys");
            var managed = await _admin.GetFromJsonAsync<List<SurveyDefinition>>("/api/surveys/managed");

            publicList!.Select(s => s.Id).Should().NotIntersectWith(ids);
            managed!.Select(s => s.Id).Should().NotIntersectWith(ids);
            managed.Should().ContainSingle(s => s.Title == InMemoryApiFactory.DemoSurveyTitle);
        }

        [Fact]
        public async Task Templates_CantBeOpenedAnsweredSharedOrChanged()
        {
            var template = await TemplateAsync("Event feedback");
            var id = template.Id;

            (await _admin.GetAsync($"/api/surveys/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
            (await _admin.GetAsync($"/api/surveys/{id}/questions")).StatusCode.Should().Be(HttpStatusCode.NotFound);
            (await _admin.PostAsJsonAsync($"/api/surveys/{id}/responses",
                new { answers = new Dictionary<string, object> { ["event_feedback_overall"] = 5 } }))
                .StatusCode.Should().Be(HttpStatusCode.NotFound);
            (await _admin.PutAsJsonAsync($"/api/surveys/{id}/sharing", new SurveySharing { Status = SurveyStatuses.Published, Listed = true }))
                .StatusCode.Should().Be(HttpStatusCode.NotFound);
            (await _admin.PutAsJsonAsync($"/api/surveys/{id}", new NewSurvey { Title = "Mine", Description = "Mine", QuestionIds = [] }))
                .StatusCode.Should().Be(HttpStatusCode.NotFound);
            (await _admin.DeleteAsync($"/api/surveys/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
            (await _admin.PostAsync($"/api/surveys/{id}/duplicate", null)).StatusCode.Should().Be(HttpStatusCode.NotFound);
            (await _admin.GetAsync($"/api/surveys/{id}/results")).StatusCode.Should().Be(HttpStatusCode.NotFound);

            (await TemplatesAsync()).Should().ContainSingle(t => t.Id == id, "the template is unchanged");
        }

        [Fact]
        public async Task UsingATemplate_MakesADraftTheCallerOwns_WithTheTemplatesQuestionsAndPages()
        {
            var template = await TemplateAsync("Course evaluation");

            var survey = await UseAsync(_professor, template.Id);

            survey.Id.Should().NotBe(template.Id);
            survey.Title.Should().Be("Course evaluation");
            survey.Status.Should().Be(SurveyStatuses.Draft);
            survey.Listed.Should().BeFalse();
            survey.IsTemplate.Should().BeFalse();
            survey.ShareCode.Should().MatchRegex("^[a-z0-9]{8}$");
            survey.OwnerName.Should().Be("professor");
            survey.QuestionIds.Should().HaveCount(10);
            survey.PageBreaks.Should().HaveCount(2);

            var questions = await _professor.GetFromJsonAsync<List<QuestionDefinition>>($"/api/surveys/{survey.Id}/questions");
            questions!.Select(q => q.Key).Should().OnlyContain(k => k.StartsWith("course_eval_"));
            var pages = SurveyPaging.Split(questions!, survey.PageBreaks);
            pages.Select(p => p.First().Key).Should().Equal("course_eval_course", "course_eval_overall", "course_eval_helped");

            var managed = await _professor.GetFromJsonAsync<List<SurveyDefinition>>("/api/surveys/managed");
            managed!.Should().ContainSingle(s => s.Id == survey.Id);

            // Each use is a new survey.
            (await UseAsync(_professor, template.Id)).Id.Should().NotBe(survey.Id);
        }

        [Fact]
        public async Task UsingAMissingTemplate_IsNotFound()
        {
            var demo = await InMemoryApiFactory.GetDemoSurveyAsync(_anonymous);

            (await _professor.PostAsync($"/api/templates/{Guid.NewGuid()}/use", null)).StatusCode.Should().Be(HttpStatusCode.NotFound);
            (await _professor.PostAsync($"/api/templates/{demo.Id}/use", null)).StatusCode.Should().Be(HttpStatusCode.NotFound,
                "a survey is not a template");
        }

        [Fact]
        public async Task TemplateQuestions_AreInTheBank_WithPrefixedKeys_AndValid()
        {
            var questions = (await _anonymous.GetFromJsonAsync<List<QuestionDefinition>>("/api/questions"))!;
            var prefixes = new[] { "course_eval_", "event_feedback_", "research_intake_", "customer_sat_" };

            var templateQuestions = questions.Where(q => prefixes.Any(q.Key.StartsWith)).ToList();

            templateQuestions.Should().HaveCount(10 + 5 + 8 + 7);
            questions.Select(q => q.Key).Should().OnlyHaveUniqueItems();
            templateQuestions.Should().OnlyContain(q => q.OwnerId == null, "templates belong to the administrators");
            templateQuestions.Should().OnlyContain(q => QuestionTypes.IsKnown(q.Type));
            var validator = new QuestionValidator();
            foreach (var question in templateQuestions)
            {
                validator.Validate(question).Valid.Should().BeTrue($"{question.Key} should be a valid question");
            }
        }

        [Fact]
        public async Task TemplateQuestions_CantBeDeletedWhileATemplateUsesThem()
        {
            var consent = await InMemoryApiFactory.GetQuestionAsync(_anonymous, "research_intake_consent");

            var response = await _admin.DeleteAsync($"/api/questions/{consent.Id}");

            response.StatusCode.Should().Be(HttpStatusCode.Conflict);
            (await response.Content.ReadAsStringAsync()).Should().Contain("Research study intake");
        }

        [Fact]
        public async Task ResearchIntake_SomeoneWhoDoesNotConsent_OnlyAnswersTheFirstPage()
        {
            var survey = await UseAsync(_professor, (await TemplateAsync("Research study intake")).Id);

            var declined = await _professor.PostAsJsonAsync($"/api/surveys/{survey.Id}/responses",
                new { answers = new Dictionary<string, object> { ["research_intake_consent"] = false } });
            var consented = await _professor.PostAsJsonAsync($"/api/surveys/{survey.Id}/responses",
                new { answers = new Dictionary<string, object> { ["research_intake_consent"] = true } });

            declined.StatusCode.Should().Be(HttpStatusCode.Created);
            consented.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await consented.Content.ReadAsStringAsync()).Should().Contain("research_intake_name").And.NotContain("research_intake_prior_details");
        }

        [Fact]
        public async Task Seeding_AgainOnTheNextStart_AddsNothing()
        {
            var questionsBefore = (await _anonymous.GetFromJsonAsync<List<QuestionDefinition>>("/api/questions"))!.Count;
            var repository = _factory.Services.GetRequiredService<ISurveyRepository>();
            var templatesBefore = repository.FindTemplates().Count();

            _factory.Services.GetRequiredService<DatabaseSeeder>().Seed();
            _factory.Services.GetRequiredService<DatabaseSeeder>().Seed();

            repository.FindTemplates().Should().HaveCount(templatesBefore).And.HaveCount(4);
            (await _anonymous.GetFromJsonAsync<List<QuestionDefinition>>("/api/questions"))!.Should().HaveCount(questionsBefore);
            repository.FindAll().Should().ContainSingle(s => s.Title == InMemoryApiFactory.DemoSurveyTitle);
        }

        [Fact]
        public async Task Templates_HaveNoShareCode()
        {
            var repository = _factory.Services.GetRequiredService<ISurveyRepository>();

            repository.AssignMissingShareCodes();

            repository.FindTemplates().Should().OnlyContain(t => t.ShareCode == null);
            (await TemplatesAsync()).Should().HaveCount(4);
        }
    }
}
