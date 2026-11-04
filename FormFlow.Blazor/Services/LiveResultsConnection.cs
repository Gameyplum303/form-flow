using System.Net;
using System.Text.Json;
using FormFlow.Data.Models;

namespace FormFlow.Blazor.Services
{
    /// <summary>Where a <see cref="LiveResultsConnection"/> is.</summary>
    public enum LiveConnectionState
    {
        /// <summary>Opening the stream for the first time.</summary>
        Connecting,

        /// <summary>Receiving updates.</summary>
        Live,

        /// <summary>The stream dropped; trying again after a pause.</summary>
        Reconnecting,

        /// <summary>The API refused the stream, so trying again won't help. See <see cref="LiveResultsConnection.StopReason"/>.</summary>
        Stopped,
    }

    /// <summary>
    /// Keeps a live results stream open for as long as it runs: it passes each update on, and when the
    /// stream drops it reconnects after a pause that doubles each time (1, 2, 4… up to 15 seconds),
    /// starting over once updates flow again. It gives up only when the API refuses the stream.
    /// </summary>
    public sealed class LiveResultsConnection(ILiveResultsClient client, TimeProvider? clock = null)
    {
        private readonly TimeProvider _clock = clock ?? TimeProvider.System;

        public TimeSpan FirstRetryDelay { get; init; } = TimeSpan.FromSeconds(1);
        public TimeSpan MaxRetryDelay { get; init; } = TimeSpan.FromSeconds(15);

        public LiveConnectionState State { get; private set; } = LiveConnectionState.Connecting;

        /// <summary>Why the connection stopped, in words for the presenter, once <see cref="State"/> is Stopped.</summary>
        public string? StopReason { get; private set; }

        /// <summary>How many times the stream has dropped since the connection started.</summary>
        public int Drops { get; private set; }

        /// <summary>Raised with each update from the API.</summary>
        public event Action<SurveyResults>? ResultsReceived;

        /// <summary>Raised whenever <see cref="State"/> changes.</summary>
        public event Action? StateChanged;

        /// <summary>Runs until cancelled or refused. Never throws for a dropped connection.</summary>
        public async Task RunAsync(Guid surveyId, CancellationToken cancellationToken)
        {
            var delay = FirstRetryDelay;
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await foreach (var results in client.WatchAsync(surveyId, cancellationToken))
                    {
                        SetState(LiveConnectionState.Live);
                        delay = FirstRetryDelay;
                        ResultsReceived?.Invoke(results);
                    }
                }
                catch (LiveResultsRefusedException refused)
                {
                    StopReason = refused.Status switch
                    {
                        HttpStatusCode.Unauthorized => "Your sign-in has ended. Sign in again to keep presenting.",
                        HttpStatusCode.Forbidden => "You can only present surveys you created.",
                        _ => "This survey is no longer available.",
                    };
                    SetState(LiveConnectionState.Stopped);
                    return;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception e) when (e is HttpRequestException or IOException or TimeoutException or OperationCanceledException
                    or JsonException)
                {
                    // Dropped; try again below.
                }

                Drops++;
                SetState(LiveConnectionState.Reconnecting);
                try
                {
                    await Task.Delay(delay, _clock, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                delay = delay * 2 > MaxRetryDelay ? MaxRetryDelay : delay * 2;
            }
        }

        private void SetState(LiveConnectionState state)
        {
            if (State != state)
            {
                State = state;
                StateChanged?.Invoke();
            }
        }
    }
}
