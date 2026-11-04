using System.Text;

namespace FormFlow.Backend.Tests.Live
{
    /// <summary>One block of a server-sent events stream: an event with its type and data, or a comment.</summary>
    public sealed record SseBlock(string? Event, string Data, string? Comment, string? Retry);

    /// <summary>
    /// Reads a text/event-stream response block by block (blocks end with a blank line), keeping the
    /// comments that SSE clients normally skip, so tests can see the keep-alives too.
    /// </summary>
    public sealed class SseTestReader(Stream stream) : IDisposable
    {
        private readonly StreamReader _reader = new(stream, Encoding.UTF8);

        /// <summary>The next block, or null when the stream ends. Fails the test if nothing arrives in time.</summary>
        public async Task<SseBlock?> NextAsync(TimeSpan? timeout = null)
        {
            using var cancel = new CancellationTokenSource(timeout ?? TimeSpan.FromSeconds(10));
            string? type = null, comment = null, retry = null;
            var data = new StringBuilder();
            var any = false;

            while (await _reader.ReadLineAsync(cancel.Token) is { } line)
            {
                if (line.Length == 0)
                {
                    if (any)
                    {
                        return new SseBlock(type, data.ToString(), comment, retry);
                    }
                    continue;
                }

                any = true;
                if (line.StartsWith(':'))
                {
                    comment = line[1..].Trim();
                }
                else if (line.StartsWith("event:"))
                {
                    type = line["event:".Length..].Trim();
                }
                else if (line.StartsWith("data:"))
                {
                    data.Append(line["data:".Length..].TrimStart());
                }
                else if (line.StartsWith("retry:"))
                {
                    retry = line["retry:".Length..].Trim();
                }
            }
            return null;
        }

        /// <summary>The next event, skipping comments and the retry field.</summary>
        public async Task<SseBlock?> NextEventAsync(TimeSpan? timeout = null)
        {
            while (await NextAsync(timeout) is { } block)
            {
                if (block.Event is not null)
                {
                    return block;
                }
            }
            return null;
        }

        public void Dispose() => _reader.Dispose();
    }
}
