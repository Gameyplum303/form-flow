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

            survey.QuestionIds.Should().HaveCount(10);
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
    }
}
