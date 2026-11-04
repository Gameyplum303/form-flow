namespace FormFlow.Backend.Live
{
    /// <summary>
    /// Tells the open live results streams (GET /api/surveys/{id}/results/live) when a survey gets a new
    /// response. It lives in the API process, so it reaches the streams this instance serves.
    /// </summary>
    public interface ILiveResultsPublisher
    {
        /// <summary>Tells everyone watching the survey that its results changed. Never blocks.</summary>
        void Publish(Guid surveyId);

        /// <summary>Starts watching a survey's results. Dispose the subscription to stop.</summary>
        LiveResultsSubscription Subscribe(Guid surveyId);

        /// <summary>How many subscriptions are watching the survey right now.</summary>
        int SubscriberCount(Guid surveyId);
    }
}
