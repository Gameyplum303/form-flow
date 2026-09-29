using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using FormFlow.Data.Models;

namespace FormFlow.Backend.Tests.Endpoints
{
    /// <summary>
    /// New surveys are private drafts until published. Published surveys are reached by their share link
    /// and appear on the public list only when listed, and closed surveys stop taking answers.
    /// </summary>
    public class SharingTests : IDisposable
    {
        private readonly InMemoryApiFactory _factory = new();
        private readonly HttpClient _anonymous;
        private readonly HttpClient _professor;

        public SharingTests()
        {
            _anonymous = _factory.CreateClient();
            _professor = _factory.CreateClient().AsProfessor();
        }

        public void Dispose() => _factory.Dispose();

        private async Task<SurveyDefinition> CreateAsync(string title = "Lab sign-up")
        {
            var question = await InMemoryApiFactory.GetQuestionAsync(_anonymous, "first_name");
            var response = await _professor.PostAsJsonAsync("/api/surveys",
                new NewSurvey { Title = title, Description = "Sign up for the lab", QuestionIds = [question.Id] });
            response.StatusCode.Should().Be(HttpStatusCode.Created);
            return (await response.Content.ReadFromJsonAsync<SurveyDefinition>())!;
        }

        private async Task<SurveyDefinition> ShareAsync(SurveyDefinition survey, string status = SurveyStatuses.Published,
            bool listed = false, DateTime? closesAt = null)
        {
            var response = await _professor.PutAsJsonAsync($"/api/surveys/{survey.Id}/sharing",
                new SurveySharing { Status = status, Listed = listed, ClosesAt = closesAt });
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            return (await response.Content.ReadFromJsonAsync<SurveyDefinition>())!;
        }

        private Task<HttpResponseMessage> AnswerAsync(HttpClient client, SurveyDefinition survey, string? respondentId = null) =>
            client.PostAsJsonAsync($"/api/surveys/{survey.Id}/responses",
                new { answers = new Dictionary<string, object> { ["first_name"] = "Ada" }, respondentId });

        private async Task<List<Guid>> PublicListAsync() =>
            (await _anonymous.GetFromJsonAsync<List<SurveyDefinition>>("/api/surveys"))!.Select(s => s.Id).ToList();

        [Fact]
        public async Task NewSurveys_ArePrivateDrafts_WithAShareCode()
        {
            var survey = await CreateAsync();

            survey.Status.Should().Be("draft");
            survey.Listed.Should().BeFalse();
            survey.ShareCode.Should().MatchRegex("^[2-9a-z]{8}$");
            (await PublicListAsync()).Should().NotContain(survey.Id);
            (await _anonymous.GetAsync($"/api/surveys/{survey.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
            (await _anonymous.GetAsync($"/api/surveys/{survey.Id}/questions")).StatusCode.Should().Be(HttpStatusCode.NotFound);
            (await _anonymous.GetAsync($"/api/share/{survey.ShareCode}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
            (await AnswerAsync(_anonymous, survey)).StatusCode.Should().Be(HttpStatusCode.NotFound);

            // Its owner can still open and try out the draft.
            (await _professor.GetAsync($"/api/surveys/{survey.Id}")).StatusCode.Should().Be(HttpStatusCode.OK);
            (await _professor.GetAsync($"/api/share/{survey.ShareCode}")).StatusCode.Should().Be(HttpStatusCode.OK);
            (await AnswerAsync(_professor, survey)).StatusCode.Should().Be(HttpStatusCode.Created);
        }

        [Fact]
        public async Task PublishedUnlistedSurveys_OpenFromTheirLink_ButStayOffThePublicList()
        {
            var survey = await ShareAsync(await CreateAsync());

            (await PublicListAsync()).Should().NotContain(survey.Id);
            var shared = await _anonymous.GetFromJsonAsync<SurveyDefinition>($"/api/share/{survey.ShareCode!.ToUpperInvariant()}");
            shared!.Id.Should().Be(survey.Id);
            (await _anonymous.GetAsync($"/api/surveys/{survey.Id}/questions")).StatusCode.Should().Be(HttpStatusCode.OK);
            (await AnswerAsync(_anonymous, survey)).StatusCode.Should().Be(HttpStatusCode.Created);
            (await _anonymous.GetAsync("/api/share/nosuchcode")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task ListedSurveys_AppearOnThePublicList_UntilTheyClose()
        {
            var survey = await ShareAsync(await CreateAsync(), listed: true);
            (await PublicListAsync()).Should().Contain(survey.Id);

            await ShareAsync(survey, listed: true, closesAt: DateTime.UtcNow.AddMinutes(-1));

            (await PublicListAsync()).Should().NotContain(survey.Id);
            (await _anonymous.GetAsync($"/api/share/{survey.ShareCode}")).StatusCode.Should().Be(HttpStatusCode.OK,
                "the link still opens, so the page can say the survey is closed");
            var closed = await AnswerAsync(_anonymous, survey);
            closed.StatusCode.Should().Be(HttpStatusCode.Conflict);
            (await closed.Content.ReadAsStringAsync()).Should().Contain("This survey is closed");
        }

        [Fact]
        public async Task ASurveyWithAFutureCloseDate_StaysOpen()
        {
            var survey = await ShareAsync(await CreateAsync(), listed: true, closesAt: DateTime.UtcNow.AddDays(7));

            (await PublicListAsync()).Should().Contain(survey.Id);
            (await AnswerAsync(_anonymous, survey)).StatusCode.Should().Be(HttpStatusCode.Created);
        }

        [Fact]
        public async Task UnpublishingASurvey_TakesItOffline()
        {
            var survey = await ShareAsync(await CreateAsync(), listed: true);

            await ShareAsync(survey, status: SurveyStatuses.Draft, listed: true);

            (await PublicListAsync()).Should().NotContain(survey.Id);
            (await _anonymous.GetAsync($"/api/share/{survey.ShareCode}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task ABrowser_CanAnswerOnce()
        {
            var survey = await ShareAsync(await CreateAsync());
            var respondent = Guid.NewGuid().ToString();

            (await _anonymous.GetFromJsonAsync<JsonElement>($"/api/surveys/{survey.Id}/answered?respondentId={respondent}"))
                .GetProperty("answered").GetBoolean().Should().BeFalse();
            (await AnswerAsync(_anonymous, survey, respondent)).StatusCode.Should().Be(HttpStatusCode.Created);

            var again = await AnswerAsync(_anonymous, survey, respondent);
            again.StatusCode.Should().Be(HttpStatusCode.Conflict);
            (await again.Content.ReadAsStringAsync()).Should().Contain("already answered");
            (await _anonymous.GetFromJsonAsync<JsonElement>($"/api/surveys/{survey.Id}/answered?respondentId={respondent}"))
                .GetProperty("answered").GetBoolean().Should().BeTrue();

            (await AnswerAsync(_anonymous, survey, Guid.NewGuid().ToString())).StatusCode.Should().Be(HttpStatusCode.Created,
                "another browser can still answer");
            (await AnswerAsync(_anonymous, survey)).StatusCode.Should().Be(HttpStatusCode.Created,
                "clients that send no respondent id aren't limited");
            (await AnswerAsync(_anonymous, survey, new string('x', 65))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task Sharing_RejectsAnUnknownStatus()
        {
            var survey = await CreateAsync();

            var response = await _professor.PutAsJsonAsync($"/api/surveys/{survey.Id}/sharing", new SurveySharing { Status = "secret" });

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task TheDemoSurvey_IsPublishedAndListed_WithAShareCode()
        {
            var demo = await InMemoryApiFactory.GetDemoSurveyAsync(_anonymous);

            demo.Status.Should().Be("published");
            demo.Listed.Should().BeTrue();
            demo.ShareCode.Should().NotBeNullOrEmpty();
        }
    }
}
