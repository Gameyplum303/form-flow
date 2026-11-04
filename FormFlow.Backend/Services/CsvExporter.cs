using System.Globalization;
using System.Text;
using FormFlow.Data.Models;

namespace FormFlow.Backend.Services
{
    /// <summary>
    /// Writes survey responses as CSV: one row per response, one column per question, except a
    /// likert grid, which gets a column per statement named <c>key[rowValue]</c>.
    /// Multiple selections are joined with "; ".
    /// </summary>
    public static class CsvExporter
    {
        public static string Export(IReadOnlyList<QuestionDefinition> questions, IEnumerable<SurveyResponse> responses)
        {
            var sb = new StringBuilder();

            var header = new List<string> { "response_id", "submitted_at", "submitted_by" };
            header.AddRange(questions.SelectMany(q => IsLikert(q)
                ? q.Rows.Select(r => $"{q.Key}[{r.Value}]")
                : [q.Key]));
            AppendRow(sb, header);

            foreach (var response in responses)
            {
                var row = new List<string>
                {
                    response.Id.ToString(),
                    response.SubmittedAt.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ"),
                    response.SubmittedBy ?? string.Empty
                };
                foreach (var question in questions)
                {
                    response.Answers.TryGetValue(question.Key, out var values);
                    if (IsLikert(question))
                    {
                        row.AddRange(question.Rows.Select(r => LikertCell(values, r.Value)));
                    }
                    else
                    {
                        row.Add(values is null ? string.Empty : string.Join("; ", values));
                    }
                }
                AppendRow(sb, row);
            }

            return sb.ToString();
        }

        private static bool IsLikert(QuestionDefinition question) =>
            string.Equals(question.Type, QuestionTypes.Likert, StringComparison.OrdinalIgnoreCase);

        /// <summary>The option picked for one statement of a likert grid, or empty when it wasn't rated.</summary>
        private static string LikertCell(List<string>? values, string row)
        {
            foreach (var value in values ?? [])
            {
                if (QuestionTypes.TryParseLikertAnswer(value, out var answeredRow, out var option) && answeredRow == row)
                {
                    return option;
                }
            }
            return string.Empty;
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
