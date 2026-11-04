using System.Net;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using FormFlow.Data.Models;

namespace FormFlow.Blazor.Services
{
    /// <summary>The API turned the live results stream down: the sign-in ended, or the survey isn't there or isn't yours.</summary>
    public class LiveResultsRefusedException(HttpStatusCode status)
        : Exception($"The live results stream was refused ({(int)status}).")
    {
        public HttpStatusCode Status { get; } = status;
    }

    /// <summary>Reads the API's live results stream, GET api/surveys/{id}/results/live.</summary>
    public interface ILiveResultsClient
    {
        /// <summary>
        /// The survey's results as the API sends them: the current ones first, then fresh ones as answers
        /// arrive. Ends when the API ends the stream. Throws <see cref="LiveResultsRefusedException"/> when the
        /// API refuses it, and <see cref="HttpRequestException"/>, <see cref="IOException"/> or
        /// <see cref="TimeoutException"/> when the connection fails or goes quiet.
        /// </summary>
        IAsyncEnumerable<SurveyResults> WatchAsync(Guid surveyId, CancellationToken cancellationToken);
    }

    /// <summary>
    /// The live results stream over the Blazor server's HttpClient, which carries the signed-in builder's
    /// bearer token like every other API call. The stream is parsed here line by line rather than with
    /// SseParser, because the API's keep-alive comments are what tell a quiet room from a dead connection.
    /// </summary>
    public class LiveResultsClient(HttpClient httpClient, AdminSession? session = null) : ILiveResultsClient
    {
        /// <summary>
        /// How long the stream may send nothing at all, not even a keep-alive, before the connection counts
        /// as dropped. The API sends a keep-alive every 15 seconds.
        /// </summary>
        public TimeSpan IdleTimeout { get; init; } = TimeSpan.FromSeconds(45);

        public async IAsyncEnumerable<SurveyResults> WatchAsync(Guid surveyId,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"api/surveys/{surveyId}/results/live");
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
            if (session is { IsSignedIn: true, Token: { } token })
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.NotFound)
            {
                throw new LiveResultsRefusedException(response.StatusCode);
            }
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var reader = new StreamReader(stream, Encoding.UTF8);
            string? eventType = null;
            var data = new StringBuilder();

            while (await ReadLineAsync(reader, cancellationToken) is { } line)
            {
                if (line.Length == 0)
                {
                    // A blank line ends an event.
                    if (eventType == "results" && data.Length > 0
                        && JsonSerializer.Deserialize<SurveyResults>(data.ToString(), JsonSerializerOptions.Web) is { } results)
                    {
                        yield return results;
                    }
                    eventType = null;
                    data.Clear();
                }
                else if (line.StartsWith("event:", StringComparison.Ordinal))
                {
                    eventType = line["event:".Length..].Trim();
                }
                else if (line.StartsWith("data:", StringComparison.Ordinal))
                {
                    data.Append(line["data:".Length..].TrimStart());
                }
                // Comments (": keep-alive"), retry and id lines only show the connection is alive.
            }
        }

        /// <summary>The next line, or null at the end of the stream; a <see cref="TimeoutException"/> after <see cref="IdleTimeout"/> of silence.</summary>
        private async Task<string?> ReadLineAsync(StreamReader reader, CancellationToken cancellationToken)
        {
            using var idle = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            idle.CancelAfter(IdleTimeout);
            try
            {
                return await reader.ReadLineAsync(idle.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException($"The live results stream sent nothing for {IdleTimeout.TotalSeconds:0} seconds.");
            }
        }
    }
}
