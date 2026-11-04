using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using FormFlow.Backend.Repositories;
using FormFlow.Data.Models;
using Microsoft.Extensions.DependencyInjection;

namespace FormFlow.Backend.Tests.Endpoints
{
    /// <summary>
    /// GET /api/surveys/{id}/results/public: the summary respondents see after answering. It only answers
    /// for a published survey whose owner turned on ShowResultsToRespondents, and it holds counts for the
    /// questions with fixed answers, never what anyone typed or who they are.
    /// </summary>
    public class RespondentResultsTests : IDisposable
    {
        private const string SecretComment = "My advisor never answers emails";
        private const string SecretName = "Ada Lovelace";
        private const string RespondentId = "browser-4f7c1d2e";

        private readonly InMemoryApiFactory _factory = new();
        private readonly HttpClient _anonymous;
        private readonly HttpClient _professor;

        public RespondentResultsTests()
        {
            _anonymous = _factory.CreateClient();
            _professor = _factory.CreateClient().AsProfessor();
        }

        public void Dispose() => _factory.Dispose();

        private async Task<SurveyDefinition> CreateAsync(params string[] keys)
        {
            var ids = new List<Guid>();
            foreach (var key in keys)
            {
                ids.Add((await InMemoryApiFactory.GetQuestionAsync(_anonymous, key)).Id);
            }
            var response = await _professor.PostAsJsonAsync("/api/surveys",
                new NewSurvey { Title = "Lecture poll", Description = "Quick check", QuestionIds = ids });
            response.StatusCode.Should().Be(HttpStatusCode.Created);
            return (await response.Content.ReadFromJsonAsync<SurveyDefinition>())!;
        }

        private async Task<SurveyDefinition> ShareAsync(SurveyDefinition survey, bool showResults, string status = SurveyStatuses.Published)
        {
            var response = await _professor.PutAsJsonAsync($"/api/surveys/{survey.Id}/sharing",
                new SurveySharing { Status = status, ShowResultsToRespondents = showResults });
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            return (await response.Content.ReadFromJsonAsync<SurveyDefinition>())!;
        }

        private async Task AnswerAsync(HttpClient client, SurveyDefinition survey, Dictionary<string, object> answers, string? respondentId = null)
        {
            var response = await client.PostAsJsonAsync($"/api/surveys/{survey.Id}/responses", new { answers, respondentId });
            response.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        private Task<HttpResponseMessage> SummaryAsync(SurveyDefinition survey) =>
            _anonymous.GetAsync($"/api/surveys/{survey.Id}/results/public");

        [Fact]
        public async Task SharingTheResults_IsSavedAndShownOnTheSurvey()
        {
            var survey = await CreateAsync("contact_method");
            survey.ShowResultsToRespondents.Should().BeFalse();

            (await ShareAsync(survey, showResults: true)).ShowResultsToRespondents.Should().BeTrue();

            var opened = await _anonymous.GetFromJsonAsync<SurveyDefinition>($"/api/share/{survey.ShareCode}");
            opened!.ShowResultsToRespondents.Should().BeTrue();
            (await ShareAsync(survey, showResults: false)).ShowResultsToRespondents.Should().BeFalse();
        }

        [Fact]
        public async Task IsNotFound_UnlessTheOwnerSharesTheResults()
        {
            var survey = await ShareAsync(await CreateAsync("contact_method"), showResults: false);

            (await SummaryAsync(survey)).StatusCode.Should().Be(HttpStatusCode.NotFound);
            // Not even the owner gets it while it's off: it is the respondents' view.
            (await _professor.GetAsync($"/api/surveys/{survey.Id}/results/public")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task IsNotFound_ForADraft_EvenWithTheResultsShared()
        {
            var survey = await ShareAsync(await CreateAsync("contact_method"), showResults: true, status: SurveyStatuses.Draft);

            (await SummaryAsync(survey)).StatusCode.Should().Be(HttpStatusCode.NotFound);
            (await _professor.GetAsync($"/api/surveys/{survey.Id}/results/public")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task IsNotFound_ForAnUnknownSurveyOrATemplate()
        {
            (await _anonymous.GetAsync($"/api/surveys/{Guid.NewGuid()}/results/public")).StatusCode.Should().Be(HttpStatusCode.NotFound);

            var repository = _factory.Services.GetRequiredService<ISurveyRepository>();
            var template = repository.FindTemplates().First();
            template.ShowResultsToRespondents = true;
            template.Status = SurveyStatuses.Published;
            repository.Update(template);
            (await _anonymous.GetAsync($"/api/surveys/{template.Id}/results/public")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task CountsTheChoiceQuestions_WithPercentagesOfThoseWhoAnswered()
        {
            var survey = await ShareAsync(await CreateAsync("contact_method", "skills", "is_student", "experience_rating"), showResults: true);
            await AnswerAsync(_anonymous, survey, new() { ["contact_method"] = "email", ["skills"] = new[] { "sql", "design" }, ["is_student"] = true });
            await AnswerAsync(_anonymous, survey, new() { ["contact_method"] = "email", ["skills"] = new[] { "sql" }, ["is_student"] = false, ["experience_rating"] = 4 });
            await AnswerAsync(_anonymous, survey, new() { ["contact_method"] = "phone", ["skills"] = new[] { "csharp" }, ["is_student"] = true });

            var summary = (await _anonymous.GetFromJsonAsync<RespondentResults>($"/api/surveys/{survey.Id}/results/public"))!;

            summary.SurveyId.Should().Be(survey.Id);
            summary.TotalResponses.Should().Be(3);
            summary.Questions.Select(q => q.Key).Should().Equal("contact_method", "skills", "is_student", "experience_rating");

            var contact = summary.Questions[0];
            contact.AnsweredCount.Should().Be(3);
            contact.MultipleAnswers.Should().BeFalse();
            contact.Options.Select(o => (o.Value, o.Count, o.Percent)).Should().Equal(("email", 2, 67), ("phone", 1, 33), ("text", 0, 0));

            // Several skills can be picked, so each share is of the people who answered and they add up to more than 100.
            var skills = summary.Questions[1];
            skills.MultipleAnswers.Should().BeTrue();
            skills.Options.Single(o => o.Value == "sql").Percent.Should().Be(67);
            skills.Options.Sum(o => o.Percent).Should().BeGreaterThan(100);

            summary.Questions[2].Options.Select(o => (o.Label, o.Count)).Should().Equal(("Yes", 2), ("No", 1));
            var rating = summary.Questions[3];
            rating.AnsweredCount.Should().Be(1);
            rating.Options.Single(o => o.Value == "4").Percent.Should().Be(100);
        }

        [Fact]
        public async Task LeavesOutFreeTextNumbersAndDates_AndNeverSaysWhoAnswered()
        {
            var survey = await ShareAsync(await CreateAsync(
                "contact_method", "first_name", "email", "age", "program_start", "study_hours", "campus_services", "comments"), showResults: true);
            var student = _factory.CreateClient().AsProfessor();
            await AnswerAsync(student, survey, new()
            {
                ["contact_method"] = "text",
                ["first_name"] = SecretName,
                ["email"] = "ada@example.com",
                ["age"] = 36,
                ["program_start"] = "2024-09-01",
                ["study_hours"] = 37,
                ["campus_services"] = new[] { "advising=5" },
                ["comments"] = SecretComment,
            }, RespondentId);

            var response = await SummaryAsync(survey);
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var body = await response.Content.ReadAsStringAsync();

            var summary = (await response.Content.ReadFromJsonAsync<RespondentResults>())!;
            summary.Questions.Select(q => q.Key).Should().Equal("contact_method");
            body.Should().NotContainAny(SecretName, SecretComment, "ada@example.com", "2024-09-01", "advising", "\"age\"", "study_hours");
            body.Should().NotContainAny(RespondentId, AdminClient.ProfessorUsername, "respondentId", "submittedBy", "ownerId", "ownerName");
        }

        [Fact]
        public async Task ASurveyWithoutChoiceQuestions_HasNothingToShow()
        {
            var survey = await ShareAsync(await CreateAsync("first_name", "comments"), showResults: true);
            await AnswerAsync(_anonymous, survey, new() { ["first_name"] = SecretName, ["comments"] = SecretComment });

            var summary = (await _anonymous.GetFromJsonAsync<RespondentResults>($"/api/surveys/{survey.Id}/results/public"))!;

            summary.TotalResponses.Should().Be(1);
            summary.Questions.Should().BeEmpty();
        }

        [Fact]
        public async Task DuplicatesKeepTheChoice()
        {
            var survey = await ShareAsync(await CreateAsync("contact_method"), showResults: true);

            var response = await _professor.PostAsync($"/api/surveys/{survey.Id}/duplicate", null);

            var copy = (await response.Content.ReadFromJsonAsync<SurveyDefinition>())!;
            copy.ShowResultsToRespondents.Should().BeTrue();
            copy.Status.Should().Be(SurveyStatuses.Draft);
        }

        [Fact]
        public async Task SurveysFromATemplate_KeepTheTemplatesChoice()
        {
            var repository = _factory.Services.GetRequiredService<ISurveyRepository>();
            var template = repository.FindTemplates().First();
            template.ShowResultsToRespondents = true;
            repository.Update(template);

            var response = await _professor.PostAsync($"/api/templates/{template.Id}/use", null);

            response.StatusCode.Should().Be(HttpStatusCode.Created);
            (await response.Content.ReadFromJsonAsync<SurveyDefinition>())!.ShowResultsToRespondents.Should().BeTrue();
        }
    }
}
