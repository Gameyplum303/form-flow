namespace FormFlow.Backend.Live
{
    /// <summary>Timings of the live results stream, from the LiveResults configuration section.</summary>
    public class LiveResultsOptions
    {
        public const string Section = "LiveResults";

        /// <summary>
        /// The shortest time between two updates. Responses that arrive closer together are sent as one
        /// update, so a room full of students answering at once doesn't flood the presenter.
        /// </summary>
        public TimeSpan MinUpdateInterval { get; set; } = TimeSpan.FromMilliseconds(500);

        /// <summary>
        /// How long the stream may stay quiet before it sends a comment, so proxies keep the connection
        /// open and the client can tell a quiet room from a dropped connection.
        /// </summary>
        public TimeSpan KeepAliveInterval { get; set; } = TimeSpan.FromSeconds(15);

        /// <summary>How long a client should wait before reconnecting, sent as the stream's retry field.</summary>
        public TimeSpan ReconnectDelay { get; set; } = TimeSpan.FromSeconds(3);
    }
}
