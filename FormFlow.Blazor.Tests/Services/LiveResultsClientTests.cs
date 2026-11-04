using System.IO.Pipelines;
using System.Net;
using System.Text;
using FluentAssertions;
using FormFlow.Blazor.Services;
using FormFlow.Blazor.Tests.Auth;
using FormFlow.Data.Models;
using RichardSzalay.MockHttp;

namespace FormFlow.Blazor.Tests.Services;

public class LiveResultsClientTests
{
    private readonly MockHttpMessageHandler _http = new();
    private readonly Guid _id = Guid.NewGuid();

    private LiveResultsClient Client(AdminSession? session = null, TimeSpan? idleTimeout = null) =>
        new(new HttpClient(_http) { BaseAddress = new Uri("http://api.test/") }, session)
        {
            IdleTimeout = idleTimeout ?? TimeSpan.FromSeconds(5),
        };

    private static StringContent EventStream(string text) => new(text, Encoding.UTF8, "text/event-stream");

    private static string ResultsEvent(int total) =>
        $"event: results\ndata: {{\"surveyId\":\"11111111-1111-1111-1111-111111111111\",\"totalResponses\":{total}}}\n\n";

    private async Task<List<SurveyResults>> ReadAllAsync(LiveResultsClient client)
    {
        var all = new List<SurveyResults>();
        await foreach (var results in client.WatchAsync(_id, CancellationToken.None))
        {
            all.Add(results);
        }
        return all;
    }

    [Fact]
    public async Task ReadsEachResultsEvent_AndSkipsCommentsAndRetry()
    {
        _http.When($"http://api.test/api/surveys/{_id}/results/live")
            .Respond(_ => EventStream("retry: 3000\n\n" + ResultsEvent(0) + ": keep-alive\n\n" + ResultsEvent(2) + "event: other\ndata: {}\n\n"));

        var all = await ReadAllAsync(Client());

        all.Select(r => r.TotalResponses).Should().Equal(0, 2);
    }

    [Fact]
    public async Task SendsTheSignedInBuildersToken()
    {
        var session = new FakeSessionStorage().CreateSession();
        await session.SignInAsync(new LoginResponse
        {
            Token = "abc.def",
            Username = "professor",
            Role = Roles.Professor,
            ExpiresAt = DateTime.UtcNow.AddHours(1),
        });
        _http.When($"http://api.test/api/surveys/{_id}/results/live")
            .WithHeaders("Authorization", "Bearer abc.def")
            .WithHeaders("Accept", "text/event-stream")
            .Respond(_ => EventStream(ResultsEvent(1)));

        (await ReadAllAsync(Client(session))).Should().ContainSingle();
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task ARefusal_SaysWhy(HttpStatusCode status)
    {
        _http.When("*").Respond(status);

        var read = () => ReadAllAsync(Client());

        (await read.Should().ThrowAsync<LiveResultsRefusedException>()).Which.Status.Should().Be(status);
    }

    [Fact]
    public async Task AServerError_IsAFailedConnection()
    {
        _http.When("*").Respond(HttpStatusCode.BadGateway);

        var read = () => ReadAllAsync(Client());

        await read.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task AStreamThatGoesSilent_TimesOut()
    {
        // The stream sends one event, then nothing at all, not even a keep-alive.
        var pipe = new Pipe();
        await pipe.Writer.WriteAsync(Encoding.UTF8.GetBytes(ResultsEvent(1)));
        _http.When("*").Respond(_ => new StreamContent(pipe.Reader.AsStream()));
        var received = new List<SurveyResults>();

        var read = async () =>
        {
            await foreach (var results in Client(idleTimeout: TimeSpan.FromMilliseconds(200)).WatchAsync(_id, CancellationToken.None))
            {
                received.Add(results);
            }
        };

        await read.Should().ThrowAsync<TimeoutException>();
        received.Should().ContainSingle();
    }

    [Fact]
    public async Task KeepAlives_KeepAQuietStreamOpen()
    {
        var pipe = new Pipe();
        _http.When("*").Respond(_ => new StreamContent(pipe.Reader.AsStream()));
        var writer = Task.Run(async () =>
        {
            for (var i = 0; i < 5; i++)
            {
                await Task.Delay(100);
                await pipe.Writer.WriteAsync(Encoding.UTF8.GetBytes(": keep-alive\n\n"));
            }
            await pipe.Writer.WriteAsync(Encoding.UTF8.GetBytes(ResultsEvent(3)));
            await pipe.Writer.CompleteAsync();
        });

        // Half a second passes in all, but never more than 300 ms without a line.
        var all = await ReadAllAsync(Client(idleTimeout: TimeSpan.FromMilliseconds(300)));

        await writer;
        all.Single().TotalResponses.Should().Be(3);
    }
}
