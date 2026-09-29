using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using FormFlow.Data.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace FormFlow.Backend.Tests.Endpoints
{
    /// <summary>
    /// Duplicating makes a new draft owned by the caller, with the same questions and pages, from any
    /// survey the caller can open: their own, any survey for an administrator, or any published one.
    /// </summary>
    public class DuplicateSurveyTests : IDisposable
    {
        private readonly InMemoryApiFactory _root = new();
        private readonly WebApplicationFactory<Program> _factory;

        public DuplicateSurveyTests()
        {
            _factory = _root.WithWebHostBuilder(b => b
                .UseSetting("Accounts:3:Username", "scientist")
                .UseSetting("Accounts:3:Password", "password")
                .UseSetting("Accounts:3:Role", "professor"));
        }

        public void Dispose()
        {
            _factory.Dispose();
            _root.Dispose();
        }

        private HttpClient Professor() => _factory.CreateClient().AsProfessor();
        private HttpClient Scientist() => _factory.CreateClient().SignedInAs("scientist", "password");

        private static async Task<SurveyDefinition> CreateAsync(HttpClient client, string title = "Lab sign-up")
        {
            var firstName = await InMemoryApiFactory.GetQuestionAsync(client, "first_name");
            var lastName = await InMemoryApiFactory.GetQuestionAsync(client, "last_name");
            var email = await InMemoryApiFactory.GetQuestionAsync(client, "email");
            var response = await client.PostAsJsonAsync("/api/surveys", new NewSurvey
            {
                Title = title,
                Description = "Sign up for the lab",
                QuestionIds = [firstName.Id, lastName.Id, email.Id],
                PageBreaks = [email.Id],
            });
            response.StatusCode.Should().Be(HttpStatusCode.Created);
            return (await response.Content.ReadFromJsonAsync<SurveyDefinition>())!;
        }

        private static async Task<SurveyDefinition> DuplicateAsync(HttpClient client, Guid id)
        {
            var response = await client.PostAsync($"/api/surveys/{id}/duplicate", null);
            response.StatusCode.Should().Be(HttpStatusCode.Created);
            response.Headers.Location!.ToString().Should().StartWith("/api/surveys/");
            return (await response.Content.ReadFromJsonAsync<SurveyDefinition>())!;
        }

        [Fact]
        public async Task Duplicate_MakesADraftCopy_WithTheSameQuestionsAndPages_AndNoResponses()
        {
            var professor = Professor();
            var original = await CreateAsync(professor);
            (await professor.PutAsJsonAsync($"/api/surveys/{original.Id}/sharing", new SurveySharing
            {
                Status = SurveyStatuses.Published,
                Listed = true,
                ClosesAt = DateTime.UtcNow.AddDays(3),
            })).StatusCode.Should().Be(HttpStatusCode.OK);
            (await _factory.CreateClient().PostAsJsonAsync($"/api/surveys/{original.Id}/responses", new
            {
                answers = new Dictionary<string, object> { ["first_name"] = "Ada", ["last_name"] = "Lovelace", ["email"] = "ada@example.com" },
            })).StatusCode.Should().Be(HttpStatusCode.Created);

            var copy = await DuplicateAsync(professor, original.Id);

            copy.Id.Should().NotBe(original.Id);
            copy.Title.Should().Be("Copy of Lab sign-up");
            copy.Description.Should().Be(original.Description);
            copy.QuestionIds.Should().Equal(original.QuestionIds);
            copy.PageBreaks.Should().Equal(original.PageBreaks).And.ContainSingle();
            copy.Status.Should().Be(SurveyStatuses.Draft);
            copy.Listed.Should().BeFalse();
            copy.ClosesAt.Should().BeNull();
            copy.ShareCode.Should().MatchRegex("^[a-z0-9]{8}$").And.NotBe(original.ShareCode);
            copy.OwnerName.Should().Be("professor");
            copy.IsTemplate.Should().BeFalse();

            (await professor.GetFromJsonAsync<List<SurveyResponse>>($"/api/surveys/{copy.Id}/responses")).Should().BeEmpty();
            (await professor.GetFromJsonAsync<List<SurveyResponse>>($"/api/surveys/{original.Id}/responses")).Should().ContainSingle();
            var managed = await professor.GetFromJsonAsync<List<SurveyDefinition>>("/api/surveys/managed");
            managed!.Select(s => s.Id).Should().Contain([original.Id, copy.Id]);
        }

        [Fact]
        public async Task Duplicate_OfAnotherBuildersPublishedSurvey_BelongsToTheCaller()
        {
            var scientist = Scientist();
            var demo = await InMemoryApiFactory.GetDemoSurveyAsync(scientist);

            var copy = await DuplicateAsync(scientist, demo.Id);

            copy.OwnerName.Should().Be("scientist");
            copy.QuestionIds.Should().Equal(demo.QuestionIds);
            (await scientist.PutAsJsonAsync($"/api/surveys/{copy.Id}", new NewSurvey
            {
                Title = "My version",
                Description = copy.Description,
                QuestionIds = copy.QuestionIds,
            })).StatusCode.Should().Be(HttpStatusCode.OK, "the copy is theirs to edit");
        }

        [Fact]
        public async Task Duplicate_OfSomeoneElsesDraft_IsNotFound()
        {
            var draft = await CreateAsync(Professor());

            var response = await Scientist().PostAsync($"/api/surveys/{draft.Id}/duplicate", null);

            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task Administrators_CanDuplicateAnyDraft()
        {
            var draft = await CreateAsync(Professor());

            var copy = await DuplicateAsync(_factory.CreateClient().AsAdmin(), draft.Id);

            copy.OwnerName.Should().Be(AdminClient.Username);
        }

        [Fact]
        public async Task Duplicate_NeedsABuilder()
        {
            var demo = await InMemoryApiFactory.GetDemoSurveyAsync(_factory.CreateClient());

            var response = await _factory.CreateClient().PostAsync($"/api/surveys/{demo.Id}/duplicate", null);

            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task Duplicate_OfAMissingSurvey_IsNotFound()
        {
            var response = await Professor().PostAsync($"/api/surveys/{Guid.NewGuid()}/duplicate", null);

            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
    }
}
