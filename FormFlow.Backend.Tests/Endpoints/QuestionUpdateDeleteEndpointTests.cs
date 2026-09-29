using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using FormFlow.Data.Models;

namespace FormFlow.Backend.Tests.Endpoints
{
    public class QuestionUpdateDeleteEndpointTests : IDisposable
    {
        private readonly InMemoryApiFactory _factory = new();
        private readonly HttpClient _client;

        public QuestionUpdateDeleteEndpointTests()
        {
            _client = _factory.CreateClient();
        }

        public void Dispose() => _factory.Dispose();

        private async Task<QuestionDefinition> CreateAsync(string key, string type = "text")
        {
            var body = new NewQuestion
            {
                Key = key,
                Label = $"Label for {key}",
                Type = type,
                Options = type == "dropdown" ? [new Option { Label = "A", Value = "a" }] : []
            };
            var response = await _client.PostAsJsonAsync("/api/questions", body);
            response.StatusCode.Should().Be(HttpStatusCode.Created);
            return (await response.Content.ReadFromJsonAsync<QuestionDefinition>())!;
        }

        private static NewQuestion EditOf(QuestionDefinition q) => new()
        {
            Key = q.Key,
            Label = q.Label,
            Type = q.Type,
            Required = q.Required,
            Options = q.Options,
            VisibleIf = q.VisibleIf,
            ValidationConfigs = q.ValidationConfigs
        };

        [Fact]
        public async Task Put_ValidChange_UpdatesAndReturnsQuestion()
        {
            var created = await CreateAsync("nickname");
            var edit = EditOf(created);
            edit.Label = "What should we call you?";
            edit.ValidationConfigs = """[{"validationType":"MaxLength","maxLength":20}]""";

            var response = await _client.PutAsJsonAsync($"/api/questions/{created.Id}", edit);

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var stored = await _client.GetFromJsonAsync<QuestionDefinition>($"/api/questions/{created.Id}");
            stored!.Label.Should().Be("What should we call you?");
            stored.ValidationConfigs.Should().Contain("MaxLength");
        }

        [Fact]
        public async Task Put_UnknownId_Returns404()
        {
            var response = await _client.PutAsJsonAsync($"/api/questions/{Guid.NewGuid()}",
                new NewQuestion { Key = "x", Label = "x", Type = "text" });

            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task Put_InvalidQuestion_Returns400()
        {
            var created = await CreateAsync("pet_name");
            var edit = EditOf(created);
            edit.Label = "";

            var response = await _client.PutAsJsonAsync($"/api/questions/{created.Id}", edit);

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await response.Content.ReadAsStringAsync()).Should().Contain("label is required");
        }

        [Fact]
        public async Task Put_KeyTakenByAnotherQuestion_Returns409()
        {
            var created = await CreateAsync("hometown");
            var edit = EditOf(created);
            edit.Key = "first_name";

            var response = await _client.PutAsJsonAsync($"/api/questions/{created.Id}", edit);

            response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        }

        [Fact]
        public async Task Put_ChangingKeyOfQuestionUsedInSurvey_Returns409()
        {
            var email = await InMemoryApiFactory.GetQuestionAsync(_client, "email");
            var edit = EditOf(email);
            edit.Key = "email_address";

            var response = await _client.PutAsJsonAsync($"/api/questions/{email.Id}", edit);

            response.StatusCode.Should().Be(HttpStatusCode.Conflict);
            (await response.Content.ReadAsStringAsync()).Should().Contain("cannot change");
        }

        [Fact]
        public async Task Put_VisibleIfOnNonYesNoQuestion_Returns400()
        {
            var created = await CreateAsync("favorite_food");
            var edit = EditOf(created);
            edit.VisibleIf = new VisibleIf { Key = "first_name", ShouldEqual = true };

            var response = await _client.PutAsJsonAsync($"/api/questions/{created.Id}", edit);

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await response.Content.ReadAsStringAsync()).Should().Contain("yes/no");
        }

        [Fact]
        public async Task Post_VisibleIfOnYesNoQuestion_IsAccepted()
        {
            var body = new NewQuestion
            {
                Key = "student_id",
                Label = "Student ID",
                Type = "text",
                VisibleIf = new VisibleIf { Key = "is_student", ShouldEqual = true }
            };

            var response = await _client.PostAsJsonAsync("/api/questions", body);

            response.StatusCode.Should().Be(HttpStatusCode.Created);
            var created = await response.Content.ReadFromJsonAsync<QuestionDefinition>();
            created!.VisibleIf!.Key.Should().Be("is_student");
        }

        [Fact]
        public async Task Post_MalformedValidationRules_Returns400()
        {
            var body = new NewQuestion { Key = "bio", Label = "Bio", Type = "text", ValidationConfigs = "not json" };

            var response = await _client.PostAsJsonAsync("/api/questions", body);

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await response.Content.ReadAsStringAsync()).Should().Contain("valid JSON");
        }

        [Fact]
        public async Task Delete_UnusedQuestion_Returns204AndRemovesIt()
        {
            var created = await CreateAsync("shoe_size");

            var response = await _client.DeleteAsync($"/api/questions/{created.Id}");

            response.StatusCode.Should().Be(HttpStatusCode.NoContent);
            (await _client.GetAsync($"/api/questions/{created.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task Delete_QuestionUsedBySurvey_Returns409()
        {
            var email = await InMemoryApiFactory.GetQuestionAsync(_client, "email");

            var response = await _client.DeleteAsync($"/api/questions/{email.Id}");

            response.StatusCode.Should().Be(HttpStatusCode.Conflict);
            (await response.Content.ReadAsStringAsync()).Should().Contain(InMemoryApiFactory.DemoSurveyTitle);
        }

        [Fact]
        public async Task Delete_UnknownQuestion_Returns404()
        {
            var response = await _client.DeleteAsync($"/api/questions/{Guid.NewGuid()}");

            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
    }
}
