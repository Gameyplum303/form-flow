using System.Runtime.CompilerServices;
using System.Threading.Channels;
using FormFlow.Blazor.Services;
using FormFlow.Data.Models;

namespace FormFlow.Blazor.Tests.Admin;

/// <summary>
/// A live results stream the test drives: each call to WatchAsync is one connection, which yields
/// what the test sends until the test drops it or refuses the next one.
/// </summary>
public sealed class FakeLiveResultsClient : ILiveResultsClient
{
    private Channel<SurveyResults> _current = Channel.CreateUnbounded<SurveyResults>();
    private Exception? _refuseWith;

    /// <summary>How many times the stream was opened.</summary>
    public int Connections { get; private set; }

    public async IAsyncEnumerable<SurveyResults> WatchAsync(Guid surveyId, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        Connections++;
        if (_refuseWith is { } refusal)
        {
            throw refusal;
        }
        var connection = _current;
        await foreach (var results in connection.Reader.ReadAllAsync(cancellationToken))
        {
            yield return results;
        }
    }

    /// <summary>Sends results on the open connection.</summary>
    public void Send(SurveyResults results) => _current.Writer.TryWrite(results);

    /// <summary>Breaks the open connection, as a dropped network would; the next one starts fresh.</summary>
    public void Drop(Exception? error = null)
    {
        var dropped = _current;
        _current = Channel.CreateUnbounded<SurveyResults>();
        dropped.Writer.TryComplete(error ?? new HttpRequestException("connection reset"));
    }

    /// <summary>Makes every later connection fail with this exception.</summary>
    public void RefuseWith(Exception error) => _refuseWith = error;
}
