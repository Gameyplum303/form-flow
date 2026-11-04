using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using FormFlow.Backend.Live;
using FormFlow.Backend.Tests.Endpoints;
using FormFlow.Data.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace FormFlow.Backend.Tests.Live
{
    /// <summary>
    /// GET /api/surveys/{id}/results/live streams a survey's results to the builders who manage it: the
    /// current results first, then fresh ones as responses arrive, with keep-alives in between.
    /// </summary>
    public class LiveResultsEndpointTests : IDisposable
    {
        private readonly InMemoryApiFactory _root = new();
        private readonly List<IDisposable> _cleanup = [];

        public void Dispose()
        {
            foreach (var item in Enumerable.Reverse(_cleanup))
            {
                item.Dispose();
            }
            _root.Dispose();
        }

        private WebApplicationFactory<Program> Api(string minUpdateInterval = "00:00:00.050", string keepAlive = "00:00:10")
        {
            var factory = _root.WithWebHostBuilder(b => b
                .UseSetting("LiveResults:MinUpdateInterval", minUpdateInterval)
                .UseSetting("LiveResults:KeepAliveInterval", keepAlive)
                .UseSetting("Accounts:3:Username", "scientist")
                .UseSetting("Accounts:3:Password", "password")
                .UseSetting("Accounts:3:Role", "professor"));
            _cleanup.Add(factory);
            return factory;
        }

        private static async Task<SurveyDefinition> CreatePublishedAsync(HttpClient professor)
        {
            var contact = await InMemoryApiFactory.GetQuestionAsync(professor, "contact_method");
            var name = await InMemoryApiFactory.GetQuestionAsync(professor, "first_name");
            var created = await professor.PostAsJsonAsync("/api/surveys",
                new NewSurvey { Title = "Lecture poll", Description = "How should we stay in touch?", QuestionIds = [contact.Id, name.Id] });
            created.StatusCode.Should().Be(HttpStatusCode.Created);
            var survey = (await created.Content.ReadFromJsonAsync<SurveyDefinition>())!;
            var shared = await professor.PutAsJsonAsync($"/api/surveys/{survey.Id}/sharing",
                new SurveySharing { Status = SurveyStatuses.Published });
            shared.StatusCode.Should().Be(HttpStatusCode.OK);
            return survey;
        }

        private static async Task AnswerAsync(HttpClient client, SurveyDefinition survey, string contact = "email")
        {
            var response = await client.PostAsJsonAsync($"/api/surveys/{survey.Id}/responses",
                new { answers = new Dictionary<string, object> { ["contact_method"] = contact, ["first_name"] = "Ada" } });
            response.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        private async Task<(HttpResponseMessage Response, SseTestReader Events)> OpenAsync(HttpClient client, Guid surveyId,
            CancellationToken cancellationToken = default)
        {
            var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, $"/api/surveys/{surveyId}/results/live"),
                HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            _cleanup.Add(response);
            var events = new SseTestReader(await response.Content.ReadAsStreamAsync(cancellationToken));
            _cleanup.Add(events);
            return (response, events);
        }

        private static SurveyResults Results(SseBlock? block)
        {
            block.Should().NotBeNull("the stream should send another event");
            block!.Event.Should().Be(LiveResultsStream.ResultsEvent);
            return JsonSerializer.Deserialize<SurveyResults>(block.Data, JsonSerializerOptions.Web)!;
        }

        [Fact]
        public async Task SendsTheCurrentResults_ThenFreshOnesWhenAResponseArrives()
        {
            var api = Api();
            var professor = api.CreateClient().AsProfessor();
            var survey = await CreatePublishedAsync(professor);
            await AnswerAsync(api.CreateClient(), survey, "phone");

            var (response, events) = await OpenAsync(professor, survey.Id);

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            response.Content.Headers.ContentType!.MediaType.Should().Be("text/event-stream");
            response.Headers.CacheControl!.NoCache.Should().BeTrue();
            (await events.NextAsync())!.Retry.Should().Be("3000");

            var first = Results(await events.NextEventAsync());
            first.SurveyId.Should().Be(survey.Id);
            first.TotalResponses.Should().Be(1);

            await AnswerAsync(api.CreateClient(), survey, "email");

            var next = Results(await events.NextEventAsync());
            next.TotalResponses.Should().Be(2);
            next.Questions.Single(q => q.Key == "contact_method").Options
                .Select(o => (o.Value, o.Count)).Should().Equal(("email", 1), ("phone", 1), ("text", 0));
        }

        [Fact]
        public async Task ABurstOfResponses_ArrivesAsOneUpdate()
        {
            var api = Api(minUpdateInterval: "00:00:01.5");
            var professor = api.CreateClient().AsProfessor();
            var survey = await CreatePublishedAsync(professor);
            var (_, events) = await OpenAsync(professor, survey.Id);
            Results(await events.NextEventAsync()).TotalResponses.Should().Be(0);

            // All five land within the update interval that started with the first event.
            var student = api.CreateClient();
            for (var i = 0; i < 5; i++)
            {
                await AnswerAsync(student, survey);
            }

            Results(await events.NextEventAsync()).TotalResponses.Should().Be(5);
        }

        [Fact]
        public async Task AQuietStream_SendsKeepAliveComments()
        {
            var api = Api(keepAlive: "00:00:00.200");
            var professor = api.CreateClient().AsProfessor();
            var survey = await CreatePublishedAsync(professor);
            var (_, events) = await OpenAsync(professor, survey.Id);

            await events.NextEventAsync();
            var quiet = await events.NextAsync(TimeSpan.FromSeconds(5));

            quiet!.Comment.Should().Be("keep-alive");
            quiet.Event.Should().BeNull();
        }

        [Fact]
        public async Task Disconnecting_Unsubscribes()
        {
            var api = Api();
            var professor = api.CreateClient().AsProfessor();
            var survey = await CreatePublishedAsync(professor);
            var publisher = api.Services.GetRequiredService<ILiveResultsPublisher>();
            using var cancel = new CancellationTokenSource();
            var (response, events) = await OpenAsync(professor, survey.Id, cancel.Token);
            await events.NextEventAsync();
            publisher.SubscriberCount(survey.Id).Should().Be(1);

            cancel.Cancel();
            response.Dispose();

            await WaitUntilAsync(() => publisher.SubscriberCount(survey.Id) == 0);
        }

        [Fact]
        public async Task DeletingTheSurvey_EndsTheStream()
        {
            var api = Api();
            var professor = api.CreateClient().AsProfessor();
            var survey = await CreatePublishedAsync(professor);
            var (_, events) = await OpenAsync(professor, survey.Id);
            await events.NextEventAsync();

            (await professor.DeleteAsync($"/api/surveys/{survey.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);

            (await events.NextEventAsync()).Should().BeNull();
            api.Services.GetRequiredService<ILiveResultsPublisher>().SubscriberCount(survey.Id).Should().Be(0);
        }

        [Fact]
        public async Task OnlyTheBuildersWhoManageTheSurvey_CanWatchIt()
        {
            var api = Api();
            var professor = api.CreateClient().AsProfessor();
            var survey = await CreatePublishedAsync(professor);
            var url = $"/api/surveys/{survey.Id}/results/live";

            (await api.CreateClient().GetAsync(url)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            (await api.CreateClient().SignedInAs("scientist", "password").GetAsync(url)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await professor.GetAsync($"/api/surveys/{Guid.NewGuid()}/results/live")).StatusCode.Should().Be(HttpStatusCode.NotFound);

            // An administrator manages every survey.
            var (adminResponse, adminEvents) = await OpenAsync(api.CreateClient().AsAdmin(), survey.Id);
            adminResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            Results(await adminEvents.NextEventAsync()).SurveyId.Should().Be(survey.Id);
        }

        private static async Task WaitUntilAsync(Func<bool> condition)
        {
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (!condition())
            {
                DateTime.UtcNow.Should().BeBefore(deadline, "the condition should hold soon");
                await Task.Delay(20);
            }
        }
    }
}
