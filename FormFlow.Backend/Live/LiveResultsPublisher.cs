namespace FormFlow.Backend.Live
{
    /// <summary>
    /// The in-process <see cref="ILiveResultsPublisher"/>, registered as a singleton. Subscriptions are
    /// kept per survey under one lock, which is held only to add, remove or signal them, so publishing
    /// never waits on a slow stream.
    /// </summary>
    public sealed class LiveResultsPublisher(TimeProvider clock) : ILiveResultsPublisher
    {
        private readonly Lock _lock = new();
        private readonly Dictionary<Guid, HashSet<LiveResultsSubscription>> _subscriptions = new();

        public void Publish(Guid surveyId)
        {
            lock (_lock)
            {
                if (_subscriptions.TryGetValue(surveyId, out var watching))
                {
                    foreach (var subscription in watching)
                    {
                        subscription.Signal();
                    }
                }
            }
        }

        public LiveResultsSubscription Subscribe(Guid surveyId)
        {
            var subscription = new LiveResultsSubscription(surveyId, Unsubscribe, clock);
            lock (_lock)
            {
                if (!_subscriptions.TryGetValue(surveyId, out var watching))
                {
                    _subscriptions[surveyId] = watching = [];
                }
                watching.Add(subscription);
            }
            return subscription;
        }

        public int SubscriberCount(Guid surveyId)
        {
            lock (_lock)
            {
                return _subscriptions.TryGetValue(surveyId, out var watching) ? watching.Count : 0;
            }
        }

        /// <summary>How many surveys have someone watching. Surveys nobody watches any more are forgotten.</summary>
        public int WatchedSurveyCount
        {
            get
            {
                lock (_lock)
                {
                    return _subscriptions.Count;
                }
            }
        }

        private void Unsubscribe(LiveResultsSubscription subscription)
        {
            lock (_lock)
            {
                if (_subscriptions.TryGetValue(subscription.SurveyId, out var watching) && watching.Remove(subscription)
                    && watching.Count == 0)
                {
                    _subscriptions.Remove(subscription.SurveyId);
                }
            }
        }
    }
}
