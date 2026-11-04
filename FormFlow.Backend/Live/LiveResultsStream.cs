using System.Globalization;
using System.Text;
using System.Text.Json;
using FormFlow.Data.Models;
using Microsoft.AspNetCore.Http.Features;

namespace FormFlow.Backend.Live
{
    /// <summary>
    /// A server-sent events response that keeps a survey's results up to date: the current results
    /// straight away, then fresh ones after each new response, no more often than
    /// <see cref="LiveResultsOptions.MinUpdateInterval"/>, with a keep-alive comment when the room is quiet.
    /// It runs until the client disconnects, the survey is deleted, or the server stops.
    /// </summary>
    /// <remarks>
    /// The stream is written by hand rather than with TypedResults.ServerSentEvents, because the
    /// keep-alive is an SSE comment line, which SseItem can't express. The format is:
    /// <code>
    /// retry: 3000
    ///
    /// event: results
    /// data: {"surveyId":"…","totalResponses":3,…}
    ///
    /// : keep-alive
    /// </code>
    /// </remarks>
    public sealed class LiveResultsStream(
        Guid surveyId,
        Func<SurveyResults?> currentResults,
        ILiveResultsPublisher publisher,
        LiveResultsOptions options,
        TimeProvider clock,
        JsonSerializerOptions json) : IResult
    {
        public const string ContentType = "text/event-stream";
        public const string ResultsEvent = "results";
        public const string KeepAliveComment = ": keep-alive";

        public async Task ExecuteAsync(HttpContext http)
        {
            var stopping = http.RequestServices.GetService<IHostApplicationLifetime>()?.ApplicationStopping ?? CancellationToken.None;
            using var stop = CancellationTokenSource.CreateLinkedTokenSource(http.RequestAborted, stopping);
            var cancellationToken = stop.Token;

            // Subscribe before reading the first results, so a response that lands in between is not missed.
            using var subscription = publisher.Subscribe(surveyId);

            var response = http.Response;
            response.ContentType = ContentType;
            response.Headers.CacheControl = "no-cache, no-store";
            // Asks nginx-style proxies not to hold the events back.
            response.Headers["X-Accel-Buffering"] = "no";
            http.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();

            try
            {
                var retry = ((long)options.ReconnectDelay.TotalMilliseconds).ToString(CultureInfo.InvariantCulture);
                await WriteAsync(response, $"retry: {retry}\n\n", cancellationToken);

                while (currentResults() is { } results)
                {
                    await WriteAsync(response,
                        $"event: {ResultsEvent}\ndata: {JsonSerializer.Serialize(results, json)}\n\n", cancellationToken);
                    var sentAt = clock.GetTimestamp();

                    while (!await subscription.WaitForChangeAsync(options.KeepAliveInterval, cancellationToken))
                    {
                        await WriteAsync(response, KeepAliveComment + "\n\n", cancellationToken);
                    }

                    // Gather the rest of a burst into this update, then take any change that came in
                    // meanwhile: the results read next include it.
                    var wait = options.MinUpdateInterval - clock.GetElapsedTime(sentAt);
                    if (wait > TimeSpan.Zero)
                    {
                        await Task.Delay(wait, clock, cancellationToken);
                    }
                    subscription.TryTakeChange();
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // The client went away or the server is stopping: nothing more to send.
            }
        }

        private static async Task WriteAsync(HttpResponse response, string text, CancellationToken cancellationToken)
        {
            await response.Body.WriteAsync(Encoding.UTF8.GetBytes(text), cancellationToken);
            await response.Body.FlushAsync(cancellationToken);
        }
    }
}
