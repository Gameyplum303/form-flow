using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using FormFlow.Data.Models;
using Microsoft.AspNetCore.Mvc;

namespace FormFlow.Backend.Tests.Endpoints
{
    public class ResponseEndpointTests : IDisposable
    {
        private readonly InMemoryApiFactory _factory = new();
        private readonly HttpClient _client;

        public ResponseEndpointTests()
        {
            _client = _factory.CreateClient();
        }

        public void Dispose() => _factory.Dispose();

        /// <summary>A complete, valid submission for the demo survey from a student.</summary>
        public static object ValidAnswers(Action<Dictionary<string, object?>>? change = null)
        {
            var answers = new Dictionary<string, object?>
            {
                ["first_name"] = "Ada",
                ["last_name"] = "Lovelace",
                ["email"] = "ada@example.com",
                ["age"] = 28,
                ["is_student"] = true,
                ["study_level"] = "master",
                ["contact_method"] = "email",
                ["subscribe_newsletter"] = false,
                ["skills"] = new[] { "csharp", "sql" },
                ["campus_preference"] = "north"
            };
            change?.Invoke(answers);
            return new { answers };
        }

        private async Task<(SurveyDefinition Survey, HttpResponseMessage Response)> SubmitAsync(object body)
        {
            var survey = await InMemoryApiFactory.GetDemoSurveyAsync(_client);
            var response = await _client.PostAsJsonAsync($"/api/surveys/{survey.Id}/responses", body);
            return (survey, response);
        }

        private static async Task<Dictionary<string, string[]>> ErrorsOf(HttpResponseMessage response)
        {
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
            return new Dictionary<string, string[]>(problem!.Errors);
        }

        [Fact]
        public async Task Submit_ValidAnswers_StoresNormalizedResponse()
        {
            var (survey, response) = await SubmitAsync(ValidAnswers());

            response.StatusCode.Should().Be(HttpStatusCode.Created);
            var stored = await _client.GetFromJsonAsync<List<SurveyResponse>>($"/api/surveys/{survey.Id}/responses");
            var answers = stored!.Single().Answers;
            answers["is_student"].Should().Equal("true");
            answers["age"].Should().Equal("28");
            answers["skills"].Should().Equal("csharp", "sql");
        }

        [Fact]
        public async Task Submit_MissingRequiredAnswer_ReturnsErrorForThatQuestion()
        {
            var (_, response) = await SubmitAsync(ValidAnswers(a => a.Remove("email")));

            var errors = await ErrorsOf(response);
            errors.Should().ContainKey("email").WhoseValue.Should().Contain("This question is required.");
        }

        [Fact]
        public async Task Submit_AnswerToHiddenQuestion_IsDropped()
        {
            var (survey, response) = await SubmitAsync(ValidAnswers(a => a["is_student"] = false));

            response.StatusCode.Should().Be(HttpStatusCode.Created);
            var stored = await _client.GetFromJsonAsync<List<SurveyResponse>>($"/api/surveys/{survey.Id}/responses");
            stored!.Single().Answers.Should().NotContainKey("campus_preference");
        }

        [Fact]
        public async Task Submit_VisibleConditionalQuestionUnanswered_IsRequired()
        {
            var (_, response) = await SubmitAsync(ValidAnswers(a => a.Remove("campus_preference")));

            var errors = await ErrorsOf(response);
            errors.Should().ContainKey("campus_preference");
        }

        [Fact]
        public async Task Submit_ValueNotInOptions_Returns400()
        {
            var (_, response) = await SubmitAsync(ValidAnswers(a => a["study_level"] = "kindergarten"));

            var errors = await ErrorsOf(response);
            errors["study_level"].Single().Should().Contain("not one of the options");
        }

        [Fact]
        public async Task Submit_NumberOutsideRule_Returns400()
        {
            var (_, response) = await SubmitAsync(ValidAnswers(a => a["age"] = 150));

            var errors = await ErrorsOf(response);
            errors.Should().ContainKey("age");
        }

        [Fact]
        public async Task Submit_NonNumericNumber_Returns400()
        {
            var (_, response) = await SubmitAsync(ValidAnswers(a => a["age"] = "old"));

            var errors = await ErrorsOf(response);
            errors["age"].Should().Contain("Answer must be a number.");
        }

        [Fact]
        public async Task Submit_UnknownQuestionKey_Returns400()
        {
            var (_, response) = await SubmitAsync(ValidAnswers(a => a["shoe_size"] = "42"));

            var errors = await ErrorsOf(response);
            errors.Should().ContainKey("shoe_size");
        }

        [Fact]
        public async Task Submit_ToUnknownSurvey_Returns404()
        {
            var response = await _client.PostAsJsonAsync($"/api/surveys/{Guid.NewGuid()}/responses", ValidAnswers());

            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task Results_SummarizeChoiceNumberAndTextAnswers()
        {
            var survey = await InMemoryApiFactory.GetDemoSurveyAsync(_client);
            await _client.PostAsJsonAsync($"/api/surveys/{survey.Id}/responses", ValidAnswers());
            await _client.PostAsJsonAsync($"/api/surveys/{survey.Id}/responses", ValidAnswers(a =>
            {
                a["first_name"] = "Grace";
                a["age"] = 40;
                a["is_student"] = false;
                a["skills"] = new[] { "csharp" };
            }));

            var results = await _client.GetFromJsonAsync<SurveyResults>($"/api/surveys/{survey.Id}/results");

            results!.TotalResponses.Should().Be(2);
            var byKey = results.Questions.ToDictionary(q => q.Key);
            byKey["skills"].Options.Single(o => o.Value == "csharp").Count.Should().Be(2);
            byKey["skills"].Options.Single(o => o.Value == "sql").Count.Should().Be(1);
            byKey["is_student"].Options.Single(o => o.Label == "Yes").Count.Should().Be(1);
            byKey["campus_preference"].AnsweredCount.Should().Be(1);
            byKey["age"].Numbers!.Average.Should().Be(34);
            byKey["first_name"].RecentAnswers.Should().BeEquivalentTo(["Grace", "Ada"]);
        }

        [Fact]
        public async Task Export_ReturnsCsvWithHeaderAndOneRowPerResponse()
        {
            var survey = await InMemoryApiFactory.GetDemoSurveyAsync(_client);
            await _client.PostAsJsonAsync($"/api/surveys/{survey.Id}/responses", ValidAnswers());

            var response = await _client.GetAsync($"/api/surveys/{survey.Id}/responses/export");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            response.Content.Headers.ContentType!.MediaType.Should().Be("text/csv");
            var lines = (await response.Content.ReadAsStringAsync()).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
            lines.Should().HaveCount(2);
            lines[0].Should().StartWith("response_id,submitted_at,first_name");
            lines[1].Should().Contain("csharp; sql");
        }
    }
}
