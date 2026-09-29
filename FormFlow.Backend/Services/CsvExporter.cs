using System.Globalization;
using System.Text;
using FormFlow.Data.Models;

namespace FormFlow.Backend.Services
{
    /// <summary>
    /// Writes survey responses as CSV: one row per response, one column per question.
    /// Multiple selections are joined with "; ".
    /// </summary>
    public static class CsvExporter
    {
        public static string Export(IReadOnlyList<QuestionDefinition> questions, IEnumerable<SurveyResponse> responses)
        {
            var sb = new StringBuilder();

            var header = new List<string> { "response_id", "submitted_at", "submitted_by" };
            header.AddRange(questions.Select(q => q.Key));
            AppendRow(sb, header);

            foreach (var response in responses)
            {
                var row = new List<string>
                {
                    response.Id.ToString(),
                    response.SubmittedAt.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ"),
                    response.SubmittedBy ?? string.Empty
                };
                row.AddRange(questions.Select(q =>
                    response.Answers.TryGetValue(q.Key, out var values) ? string.Join("; ", values) : string.Empty));
                AppendRow(sb, row);
            }

            return sb.ToString();
        }

        private static void AppendRow(StringBuilder sb, IEnumerable<string> cells)
        {
            sb.AppendJoin(',', cells.Select(Escape));
            sb.Append("\r\n");
        }

        /// <summary>
        /// Quotes a cell when it contains a delimiter, quote or line break, and neutralizes
        /// leading characters that spreadsheet apps would run as a formula.
        /// </summary>
        public static string Escape(string value)
        {
            var looksLikeFormula = value.Length > 0 && "=+-@\t\r".Contains(value[0])
                && !decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out _);
            if (looksLikeFormula)
            {
                value = "'" + value;
            }

            if (value.IndexOfAny([',', '"', '\n', '\r']) >= 0)
            {
                return "\"" + value.Replace("\"", "\"\"") + "\"";
            }
            return value;
        }
    }
}
