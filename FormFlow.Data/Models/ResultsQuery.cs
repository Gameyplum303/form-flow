using System.Globalization;

namespace FormFlow.Data.Models
{
    /// <summary>
    /// Narrows a survey's results: only responses with certain answers, only responses sent in a
    /// date range, and optionally split by the answer to one question. Sent as query parameters
    /// to the results and CSV export endpoints.
    /// </summary>
    public class ResultsQuery
    {
        /// <summary>Only responses that gave all of these answers.</summary>
        public List<AnswerFilter> Filters { get; set; } = new();

        /// <summary>Only responses sent at or after this time (UTC).</summary>
        public DateTime? From { get; set; }

        /// <summary>Only responses sent before this time (UTC).</summary>
        public DateTime? To { get; set; }

        /// <summary>The key of a question to split the results by.</summary>
        public string? CompareBy { get; set; }

        /// <summary>Minutes ahead of UTC (-300 for US Eastern in winter), so the timeline counts local days.</summary>
        public int UtcOffsetMinutes { get; set; }

        public bool IsEmpty => Filters.Count == 0 && From is null && To is null && string.IsNullOrEmpty(CompareBy) && UtcOffsetMinutes == 0;

        /// <summary>The query string for these settings, without the leading "?"; empty when nothing is set.</summary>
        public string ToQueryString()
        {
            var parts = new List<string>();
            parts.AddRange(Filters.Select(f => "filter=" + Uri.EscapeDataString($"{f.Key}:{f.Value}")));
            if (From is { } from)
            {
                parts.Add("from=" + Uri.EscapeDataString(Utc(from)));
            }
            if (To is { } to)
            {
                parts.Add("to=" + Uri.EscapeDataString(Utc(to)));
            }
            if (!string.IsNullOrEmpty(CompareBy))
            {
                parts.Add("compareBy=" + Uri.EscapeDataString(CompareBy));
            }
            if (UtcOffsetMinutes != 0)
            {
                parts.Add("utcOffset=" + UtcOffsetMinutes.ToString(CultureInfo.InvariantCulture));
            }
            return string.Join("&", parts);
        }

        private static string Utc(DateTime time) =>
            DateTime.SpecifyKind(time, DateTimeKind.Utc).ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
    }

    /// <summary>Keep responses whose answer to <see cref="Key"/> is, or includes, <see cref="Value"/>.</summary>
    public record AnswerFilter(string Key, string Value);
}
