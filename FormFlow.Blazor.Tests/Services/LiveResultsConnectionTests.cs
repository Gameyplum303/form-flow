using System.Net;
using FluentAssertions;
using FormFlow.Blazor.Services;
using FormFlow.Blazor.Tests.Admin;
using FormFlow.Data.Models;

namespace FormFlow.Blazor.Tests.Services;

public class LiveResultsConnectionTests
{
    private readonly FakeLiveResultsClient _client = new();
    private readonly List<int> _totals = [];
    private readonly List<LiveConnectionState> _states = [];
    private readonly LiveResultsConnection _connection;

    public LiveResultsConnectionTests()
    {
        _connection = new LiveResultsConnection(_client)
        {
            FirstRetryDelay = TimeSpan.FromMilliseconds(10),
            MaxRetryDelay = TimeSpan.FromMilliseconds(40),
        };
        _connection.ResultsReceived += r => { lock (_totals) { _totals.Add(r.TotalResponses); } };
        _connection.StateChanged += () => { lock (_states) { _states.Add(_connection.State); } };
    }

    private static SurveyResults Results(int total) => new() { TotalResponses = total };

    private static async Task EventuallyAsync(Action assertion)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (true)
        {
            try
            {
                assertion();
                return;
            }
            catch when (DateTime.UtcNow < deadline)
            {
                await Task.Delay(10);
            }
        }
    }

    [Fact]
    public async Task PassesUpdatesOn_AndIsLive()
    {
        using var stop = new CancellationTokenSource();
        var running = _connection.RunAsync(Guid.NewGuid(), stop.Token);
        _connection.State.Should().Be(LiveConnectionState.Connecting);

        _client.Send(Results(0));
        _client.Send(Results(1));

        await EventuallyAsync(() => _totals.Should().Equal(0, 1));
        _connection.State.Should().Be(LiveConnectionState.Live);
        stop.Cancel();
        await running;
    }

    [Fact]
    public async Task ReconnectsAfterADrop_AndCarriesOn()
    {
        using var stop = new CancellationTokenSource();
        var running = _connection.RunAsync(Guid.NewGuid(), stop.Token);
        _client.Send(Results(1));
        await EventuallyAsync(() => _connection.State.Should().Be(LiveConnectionState.Live));

        _client.Drop();
        await EventuallyAsync(() => _client.Connections.Should().Be(2));
        _client.Send(Results(4));

        await EventuallyAsync(() => _totals.Should().Equal(1, 4));
        _states.Should().ContainInOrder(LiveConnectionState.Live, LiveConnectionState.Reconnecting, LiveConnectionState.Live);
        _connection.Drops.Should().Be(1);
        stop.Cancel();
        await running;
    }

    [Theory]
    [InlineData(typeof(IOException))]
    [InlineData(typeof(TimeoutException))]
    public async Task OtherConnectionFailures_AlsoReconnect(Type failure)
    {
        using var stop = new CancellationTokenSource();
        var running = _connection.RunAsync(Guid.NewGuid(), stop.Token);

        _client.Drop((Exception)Activator.CreateInstance(failure)!);

        await EventuallyAsync(() => _client.Connections.Should().BeGreaterThanOrEqualTo(2));
        stop.Cancel();
        await running;
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "Your sign-in has ended.")]
    [InlineData(HttpStatusCode.Forbidden, "You can only present surveys you created.")]
    [InlineData(HttpStatusCode.NotFound, "This survey is no longer available.")]
    public async Task StopsWhenRefused_AndSaysWhy(HttpStatusCode status, string reason)
    {
        _client.RefuseWith(new LiveResultsRefusedException(status));

        await _connection.RunAsync(Guid.NewGuid(), CancellationToken.None);

        _connection.State.Should().Be(LiveConnectionState.Stopped);
        _connection.StopReason.Should().StartWith(reason);
        _client.Connections.Should().Be(1);
    }

    [Fact]
    public async Task Cancelling_EndsTheRunQuietly()
    {
        using var stop = new CancellationTokenSource();
        var running = _connection.RunAsync(Guid.NewGuid(), stop.Token);

        stop.Cancel();

        await running.WaitAsync(TimeSpan.FromSeconds(5));
        _connection.State.Should().NotBe(LiveConnectionState.Stopped);
    }
}
