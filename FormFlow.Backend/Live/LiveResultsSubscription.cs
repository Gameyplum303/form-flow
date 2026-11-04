using System.Threading.Channels;

namespace FormFlow.Backend.Live
{
    /// <summary>
    /// One watcher of a survey's results, from <see cref="ILiveResultsPublisher.Subscribe"/>. It holds at
    /// most one pending change: however many responses arrive before the watcher looks, it sees one
    /// change and reads the latest results, so a burst of answers costs one update.
    /// </summary>
    public sealed class LiveResultsSubscription : IDisposable
    {
        private readonly Channel<bool> _changes = Channel.CreateBounded<bool>(new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.DropWrite,
            SingleReader = true,
        });

        private readonly Action<LiveResultsSubscription> _unsubscribe;
        private readonly TimeProvider _clock;
        private int _disposed;

        internal LiveResultsSubscription(Guid surveyId, Action<LiveResultsSubscription> unsubscribe, TimeProvider clock)
        {
            SurveyId = surveyId;
            _unsubscribe = unsubscribe;
            _clock = clock;
        }

        public Guid SurveyId { get; }

        /// <summary>Marks a change. A change already waiting absorbs this one.</summary>
        internal void Signal() => _changes.Writer.TryWrite(true);

        /// <summary>Takes the pending change, if there is one, without waiting.</summary>
        public bool TryTakeChange() => _changes.Reader.TryRead(out _);

        /// <summary>
        /// Waits for a change and takes it. Returns false when <paramref name="timeout"/> passes first,
        /// and throws <see cref="OperationCanceledException"/> when <paramref name="cancellationToken"/> is cancelled.
        /// </summary>
        public async Task<bool> WaitForChangeAsync(TimeSpan timeout, CancellationToken cancellationToken)
        {
            ObjectDisposedException.ThrowIf(_disposed != 0, this);
            if (TryTakeChange())
            {
                return true;
            }

            using var timer = new CancellationTokenSource(timeout, _clock);
            using var either = CancellationTokenSource.CreateLinkedTokenSource(timer.Token, cancellationToken);
            try
            {
                while (await _changes.Reader.WaitToReadAsync(either.Token))
                {
                    if (TryTakeChange())
                    {
                        return true;
                    }
                }
                throw new ObjectDisposedException(nameof(LiveResultsSubscription));
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return false;
            }
        }

        /// <summary>Stops watching. Safe to call more than once.</summary>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                _unsubscribe(this);
                _changes.Writer.TryComplete();
            }
        }
    }
}
