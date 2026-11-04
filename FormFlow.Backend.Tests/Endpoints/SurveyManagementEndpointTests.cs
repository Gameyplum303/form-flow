using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using FormFlow.Backend.Repositories;
using FormFlow.Data.Models;
using Microsoft.Extensions.DependencyInjection;

namespace FormFlow.Backend.Tests.Endpoints
{
    public class SurveyManagementEndpointTests : IDisposable
    {
        private readonly InMemoryApiFactory _factory = new();
        private readonly HttpClient _client;

        public SurveyManagementEndpointTests()
        {
            _client = _factory.CreateClient().AsAdmin();
        }

        public void Dispose() => _factory.Dispose();

        [Fact]
        public async Task DemoSurvey_IsSeeded_WithAllSampleQuestions()
        {
            var survey = await InMemoryApiFactory.GetDemoSurveyAsync(_client);

            survey.QuestionIds.Should().HaveCount(16);
        }

        [Fact]
        public async Task GetQuestions_ReturnsQuestionsInSurveyOrder()
        {
            var survey = await InMemoryApiFactory.GetDemoSurveyAsync(_client);

            var questions = await _client.GetFromJsonAsync<List<QuestionDefinition>>($"/api/surveys/{survey.Id}/questions");

            questions!.Select(q => q.Id).Should().Equal(survey.QuestionIds);
        }

        [Fact]
        public async Task Post_UnknownQuestionId_Returns400()
        {
            var response = await _client.PostAsJsonAsync("/api/surveys", new NewSurvey
            {
                Title = "T",
                Description = "D",
                QuestionIds = [Guid.NewGuid()]
            });

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await response.Content.ReadAsStringAsync()).Should().Contain("Unknown question ids");
        }

        [Fact]
        public async Task Post_MissingDescription_ExplainsWhichFieldIsMissing()
        {
            var email = await InMemoryApiFactory.GetQuestionAsync(_client, "email");

            var response = await _client.PostAsJsonAsync("/api/surveys", new NewSurvey
            {
                Title = "T",
                Description = " ",
                QuestionIds = [email.Id]
            });

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await response.Content.ReadAsStringAsync()).Should().Contain("Description is required");
        }

        [Fact]
        public async Task Post_DuplicateQuestion_Returns400()
        {
            var email = await InMemoryApiFactory.GetQuestionAsync(_client, "email");

            var response = await _client.PostAsJsonAsync("/api/surveys", new NewSurvey
            {
                Title = "T",
                Description = "D",
                QuestionIds = [email.Id, email.Id]
            });

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task Put_UpdatesTitleAndReordersQuestions()
        {
            var survey = await InMemoryApiFactory.GetDemoSurveyAsync(_client);
            var reordered = survey.QuestionIds.AsEnumerable().Reverse().ToList();

            var response = await _client.PutAsJsonAsync($"/api/surveys/{survey.Id}", new NewSurvey
            {
                Title = "Renamed",
                Description = survey.Description,
                QuestionIds = reordered
            });

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var stored = await _client.GetFromJsonAsync<SurveyDefinition>($"/api/surveys/{survey.Id}");
            stored!.Title.Should().Be("Renamed");
            stored.QuestionIds.Should().Equal(reordered);
            stored.CreatedAt.Should().BeCloseTo(survey.CreatedAt, TimeSpan.FromSeconds(1));
        }

        [Fact]
        public async Task Put_UnknownSurvey_Returns404()
        {
            var email = await InMemoryApiFactory.GetQuestionAsync(_client, "email");

            var response = await _client.PutAsJsonAsync($"/api/surveys/{Guid.NewGuid()}", new NewSurvey
            {
                Title = "T",
                Description = "D",
                QuestionIds = [email.Id]
            });

            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task Delete_RemovesSurveyAndItsResponses()
        {
            var survey = await InMemoryApiFactory.GetDemoSurveyAsync(_client);
            await _client.PostAsJsonAsync($"/api/surveys/{survey.Id}/responses", ResponseEndpointTests.ValidAnswers());

            var response = await _client.DeleteAsync($"/api/surveys/{survey.Id}");

            response.StatusCode.Should().Be(HttpStatusCode.NoContent);
            (await _client.GetAsync($"/api/surveys/{survey.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
            var responses = _factory.Services.GetRequiredService<IResponseRepository>();
            responses.CountBySurveyId(survey.Id).Should().Be(0);
        }

        [Fact]
        public async Task Delete_UnknownSurvey_Returns404()
        {
            var response = await _client.DeleteAsync($"/api/surveys/{Guid.NewGuid()}");

            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task PageBreaks_RoundTrip_KeepingOnlyQuestionsThatStartAPage()
        {
            var first = await InMemoryApiFactory.GetQuestionAsync(_client, "first_name");
            var last = await InMemoryApiFactory.GetQuestionAsync(_client, "last_name");
            var email = await InMemoryApiFactory.GetQuestionAsync(_client, "email");
            var age = await InMemoryApiFactory.GetQuestionAsync(_client, "age");

            // The first question, a question that isn't in the survey, and a repeat are all dropped.
            var created = await _client.PostAsJsonAsync("/api/surveys", new NewSurvey
            {
                Title = "Paged",
                Description = "Two pages",
                QuestionIds = [first.Id, last.Id, email.Id],
                PageBreaks = [email.Id, first.Id, age.Id, email.Id, Guid.NewGuid()],
            });
            created.StatusCode.Should().Be(HttpStatusCode.Created);
            var survey = (await created.Content.ReadFromJsonAsync<SurveyDefinition>())!;
            survey.PageBreaks.Should().Equal(email.Id);
            (await _client.GetFromJsonAsync<SurveyDefinition>($"/api/surveys/{survey.Id}"))!.PageBreaks.Should().Equal(email.Id);

            var updated = await _client.PutAsJsonAsync($"/api/surveys/{survey.Id}", new NewSurvey
            {
                Title = "Paged",
                Description = "Three pages",
                QuestionIds = [first.Id, last.Id, email.Id, age.Id],
                PageBreaks = [age.Id, last.Id],
            });
            updated.StatusCode.Should().Be(HttpStatusCode.OK);
            var stored = await _client.GetFromJsonAsync<SurveyDefinition>($"/api/surveys/{survey.Id}");
            stored!.PageBreaks.Should().Equal(last.Id, age.Id);

            // Leaving page breaks out makes it one page again.
            (await _client.PutAsJsonAsync($"/api/surveys/{survey.Id}", new { title = "Paged", description = "One page", questionIds = new[] { first.Id, last.Id } }))
                .StatusCode.Should().Be(HttpStatusCode.OK);
            (await _client.GetFromJsonAsync<SurveyDefinition>($"/api/surveys/{survey.Id}"))!.PageBreaks.Should().BeEmpty();
        }

        [Fact]
        public async Task DemoSurvey_HasNoPageBreaks()
        {
            var survey = await InMemoryApiFactory.GetDemoSurveyAsync(_client);

            survey.PageBreaks.Should().BeEmpty();
            survey.IsTemplate.Should().BeFalse();
        }
    }
}
