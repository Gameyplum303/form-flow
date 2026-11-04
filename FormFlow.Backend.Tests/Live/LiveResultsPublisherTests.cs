using FluentAssertions;
using FormFlow.Backend.Live;

namespace FormFlow.Backend.Tests.Live
{
    /// <summary>
    /// The publisher wakes the subscriptions of one survey, folds a burst of responses into one change,
    /// and forgets subscriptions once they are disposed, however many threads use it at once.
    /// </summary>
    public class LiveResultsPublisherTests
    {
        private static readonly TimeSpan Short = TimeSpan.FromMilliseconds(50);
        private static readonly TimeSpan Long = TimeSpan.FromSeconds(5);

        private readonly LiveResultsPublisher _publisher = new(TimeProvider.System);
        private readonly Guid _survey = Guid.NewGuid();

        [Fact]
        public async Task Publish_WakesEverySubscriptionToThatSurvey()
        {
            using var first = _publisher.Subscribe(_survey);
            using var second = _publisher.Subscribe(_survey);

            var waiting = first.WaitForChangeAsync(Long, CancellationToken.None);
            _publisher.Publish(_survey);

            (await waiting).Should().BeTrue();
            second.TryTakeChange().Should().BeTrue();
        }

        [Fact]
        public async Task Publish_LeavesOtherSurveysAlone()
        {
            using var other = _publisher.Subscribe(Guid.NewGuid());

            _publisher.Publish(_survey);

            (await other.WaitForChangeAsync(Short, CancellationToken.None)).Should().BeFalse();
        }

        [Fact]
        public void Publish_WithNobodyWatching_DoesNothing()
        {
            var publish = () => _publisher.Publish(_survey);

            publish.Should().NotThrow();
            _publisher.WatchedSurveyCount.Should().Be(0);
        }

        [Fact]
        public async Task ABurstOfResponses_IsOneChange()
        {
            using var subscription = _publisher.Subscribe(_survey);

            for (var i = 0; i < 25; i++)
            {
                _publisher.Publish(_survey);
            }

            (await subscription.WaitForChangeAsync(Short, CancellationToken.None)).Should().BeTrue();
            subscription.TryTakeChange().Should().BeFalse();
            (await subscription.WaitForChangeAsync(Short, CancellationToken.None)).Should().BeFalse();
        }

        [Fact]
        public async Task ChangesBeforeTheWait_AreNotLost()
        {
            using var subscription = _publisher.Subscribe(_survey);
            _publisher.Publish(_survey);

            (await subscription.WaitForChangeAsync(Short, CancellationToken.None)).Should().BeTrue();
        }

        [Fact]
        public async Task Waiting_TimesOutWithFalse()
        {
            using var subscription = _publisher.Subscribe(_survey);

            var started = DateTime.UtcNow;
            (await subscription.WaitForChangeAsync(TimeSpan.FromMilliseconds(100), CancellationToken.None)).Should().BeFalse();
            (DateTime.UtcNow - started).Should().BeGreaterThanOrEqualTo(TimeSpan.FromMilliseconds(90));
        }

        [Fact]
        public async Task Waiting_StopsWhenCancelled()
        {
            using var subscription = _publisher.Subscribe(_survey);
            using var cancel = new CancellationTokenSource(Short);

            var wait = () => subscription.WaitForChangeAsync(Long, cancel.Token);

            await wait.Should().ThrowAsync<OperationCanceledException>();
        }

        [Fact]
        public async Task Disposing_Unsubscribes_AndForgetsTheSurvey()
        {
            var first = _publisher.Subscribe(_survey);
            var second = _publisher.Subscribe(_survey);
            _publisher.SubscriberCount(_survey).Should().Be(2);

            first.Dispose();
            _publisher.SubscriberCount(_survey).Should().Be(1);
            second.Dispose();
            second.Dispose();

            _publisher.SubscriberCount(_survey).Should().Be(0);
            _publisher.WatchedSurveyCount.Should().Be(0);
            var wait = () => first.WaitForChangeAsync(Short, CancellationToken.None);
            await wait.Should().ThrowAsync<ObjectDisposedException>();
        }

        [Fact]
        public async Task DisposingWhileWaiting_EndsTheWait()
        {
            var subscription = _publisher.Subscribe(_survey);
            var waiting = subscription.WaitForChangeAsync(Long, CancellationToken.None);

            subscription.Dispose();

            Func<Task> wait = () => waiting;
            await wait.Should().ThrowAsync<ObjectDisposedException>();
        }

        [Fact]
        public async Task ManyThreadsSubscribingPublishingAndLeaving_LeaveNothingBehind()
        {
            var surveys = Enumerable.Range(0, 4).Select(_ => Guid.NewGuid()).ToArray();
            var woken = 0;

            await Parallel.ForAsync(0, 200, async (i, cancellationToken) =>
            {
                var survey = surveys[i % surveys.Length];
                using var subscription = _publisher.Subscribe(survey);
                _publisher.Publish(survey);
                if (await subscription.WaitForChangeAsync(Long, cancellationToken))
                {
                    Interlocked.Increment(ref woken);
                }
                _publisher.Publish(surveys[(i + 1) % surveys.Length]);
            });

            woken.Should().Be(200);
            surveys.Should().AllSatisfy(s => _publisher.SubscriberCount(s).Should().Be(0));
            _publisher.WatchedSurveyCount.Should().Be(0);
        }
    }
}
