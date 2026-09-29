using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using FormFlow.Data.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace FormFlow.Backend.Tests.Endpoints
{
    /// <summary>
    /// Professors manage the surveys and questions they created, administrators manage everything,
    /// and the seeded demo data belongs to the administrators.
    /// </summary>
    public class OwnershipTests : IDisposable
    {
        private readonly InMemoryApiFactory _root = new();
        private readonly WebApplicationFactory<Program> _factory;

        public OwnershipTests()
        {
            // A second professor, to check professors can't reach each other's surveys.
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
        private HttpClient Admin() => _factory.CreateClient().AsAdmin();

        private static async Task<QuestionDefinition> CreateQuestionAsync(HttpClient client, string key)
        {
            var response = await client.PostAsJsonAsync("/api/questions",
                new NewQuestion { Key = key, Label = $"Question {key}", Type = "text" });
            response.StatusCode.Should().Be(HttpStatusCode.Created);
            return (await response.Content.ReadFromJsonAsync<QuestionDefinition>())!;
        }

        private static async Task<SurveyDefinition> CreateSurveyAsync(HttpClient client, string title, params Guid[] questionIds)
        {
            var response = await client.PostAsJsonAsync("/api/surveys",
                new NewSurvey { Title = title, Description = "About things", QuestionIds = questionIds.ToList() });
            response.StatusCode.Should().Be(HttpStatusCode.Created);
            return (await response.Content.ReadFromJsonAsync<SurveyDefinition>())!;
        }

        [Fact]
        public async Task Professor_OwnsWhatTheyCreate_AndCanUseTheSharedQuestionBank()
        {
            var professor = Professor();
            var seeded = await InMemoryApiFactory.GetQuestionAsync(professor, "first_name");

            var question = await CreateQuestionAsync(professor, "lab_group");
            var survey = await CreateSurveyAsync(professor, "Lab feedback", seeded.Id, question.Id);

            question.OwnerName.Should().Be("professor");
            question.OwnerId.Should().NotBeNull();
            survey.OwnerName.Should().Be("professor");
            survey.OwnerId.Should().Be(question.OwnerId);
        }

        [Fact]
        public async Task Professor_ManagesTheirOwnSurvey_FromEditToResultsToDelete()
        {
            var professor = Professor();
            var seeded = await InMemoryApiFactory.GetQuestionAsync(professor, "first_name");
            var survey = await CreateSurveyAsync(professor, "Lab feedback", seeded.Id);
            (await professor.PutAsJsonAsync($"/api/surveys/{survey.Id}/sharing",
                new SurveySharing { Status = SurveyStatuses.Published })).StatusCode.Should().Be(HttpStatusCode.OK);
            await _factory.CreateClient().PostAsJsonAsync($"/api/surveys/{survey.Id}/responses",
                new { answers = new Dictionary<string, object> { ["first_name"] = "Ada" } });

            var edit = await professor.PutAsJsonAsync($"/api/surveys/{survey.Id}",
                new NewSurvey { Title = "Lab feedback, week 2", Description = "About things", QuestionIds = [seeded.Id] });
            edit.StatusCode.Should().Be(HttpStatusCode.OK);
            var edited = (await edit.Content.ReadFromJsonAsync<SurveyDefinition>())!;
            edited.OwnerName.Should().Be("professor", "editing keeps the owner");
            edited.Status.Should().Be(SurveyStatuses.Published, "editing keeps the sharing settings");

            (await professor.GetFromJsonAsync<SurveyResults>($"/api/surveys/{survey.Id}/results"))!.TotalResponses.Should().Be(1);
            (await professor.GetAsync($"/api/surveys/{survey.Id}/responses")).StatusCode.Should().Be(HttpStatusCode.OK);
            (await professor.GetAsync($"/api/surveys/{survey.Id}/responses/export")).StatusCode.Should().Be(HttpStatusCode.OK);
            (await professor.DeleteAsync($"/api/surveys/{survey.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        [Theory]
        [InlineData("PUT", "/api/surveys/{survey}")]
        [InlineData("DELETE", "/api/surveys/{survey}")]
        [InlineData("GET", "/api/surveys/{survey}/results")]
        [InlineData("GET", "/api/surveys/{survey}/responses")]
        [InlineData("GET", "/api/surveys/{survey}/responses/export")]
        [InlineData("PUT", "/api/surveys/{survey}/sharing")]
        [InlineData("PUT", "/api/questions/{question}")]
        [InlineData("DELETE", "/api/questions/{question}")]
        public async Task Professor_CannotTouchAnotherProfessorsWork_OrTheDemoData(string method, string path)
        {
            var scientist = Scientist();
            var question = await CreateQuestionAsync(scientist, "scientist_only");
            var theirSurvey = await CreateSurveyAsync(scientist, "Field study", question.Id);
            var demoSurvey = await InMemoryApiFactory.GetDemoSurveyAsync(scientist);
            var demoQuestion = await InMemoryApiFactory.GetQuestionAsync(scientist, "first_name");

            foreach (var (surveyId, questionId) in new[] { (theirSurvey.Id, question.Id), (demoSurvey.Id, demoQuestion.Id) })
            {
                var request = new HttpRequestMessage(new HttpMethod(method),
                    path.Replace("{survey}", surveyId.ToString()).Replace("{question}", questionId.ToString()));
                if (method == "PUT")
                {
                    request.Content = path.Contains("questions")
                        ? JsonContent.Create(new NewQuestion { Key = "changed", Label = "Changed", Type = "text" })
                        : JsonContent.Create(new NewSurvey { Title = "Changed", Description = "Changed", QuestionIds = [questionId] });
                }

                var response = await Professor().SendAsync(request);

                response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
                (await response.Content.ReadAsStringAsync()).Should().Contain("You can only manage");
            }
        }

        [Fact]
        public async Task ManagedSurveys_AreAllOfThemForAnAdmin_AndTheirOwnForAProfessor()
        {
            var question = await CreateQuestionAsync(Professor(), "managed_q");
            await CreateSurveyAsync(Professor(), "Mine", question.Id);
            await CreateSurveyAsync(Scientist(), "Theirs", question.Id);

            var professorsList = await Professor().GetFromJsonAsync<List<SurveyDefinition>>("/api/surveys/managed");
            var adminsList = await Admin().GetFromJsonAsync<List<SurveyDefinition>>("/api/surveys/managed");

            professorsList!.Select(s => s.Title).Should().Equal("Mine");
            adminsList!.Select(s => s.Title).Should().BeEquivalentTo(["Mine", "Theirs", InMemoryApiFactory.DemoSurveyTitle]);
        }

        [Fact]
        public async Task Admin_CanManageAProfessorsSurvey()
        {
            var question = await CreateQuestionAsync(Professor(), "admin_can");
            var survey = await CreateSurveyAsync(Professor(), "Professor's survey", question.Id);
            var admin = Admin();

            (await admin.GetAsync($"/api/surveys/{survey.Id}/results")).StatusCode.Should().Be(HttpStatusCode.OK);
            (await admin.PutAsJsonAsync($"/api/questions/{question.Id}",
                new NewQuestion { Key = "admin_can", Label = "Renamed by an admin", Type = "text" })).StatusCode.Should().Be(HttpStatusCode.OK);
            (await admin.DeleteAsync($"/api/surveys/{survey.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        [Fact]
        public async Task Responses_RecordASignedInRespondent_AndTheCsvShowsThem()
        {
            var admin = Admin();
            var survey = await InMemoryApiFactory.GetDemoSurveyAsync(admin);
            // Students answer without an account; a professor trying out a survey is signed in.
            var signedIn = await Professor().PostAsJsonAsync($"/api/surveys/{survey.Id}/responses", ResponseEndpointTests.ValidAnswers());
            var anonymous = await _factory.CreateClient().PostAsJsonAsync($"/api/surveys/{survey.Id}/responses", ResponseEndpointTests.ValidAnswers());

            (await signedIn.Content.ReadFromJsonAsync<SurveyResponse>())!.SubmittedBy.Should().Be("professor");
            (await anonymous.Content.ReadFromJsonAsync<SurveyResponse>())!.SubmittedBy.Should().BeNull();
            var csv = await admin.GetStringAsync($"/api/surveys/{survey.Id}/responses/export");
            csv.Split("\r\n")[0].Should().StartWith("response_id,submitted_at,submitted_by,first_name");
            csv.Should().Contain(",professor,");
        }
    }
}
