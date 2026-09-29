using FormFlow.Data.Models;

namespace FormFlow.Data.Services
{
    /// <summary>
    /// Splits a survey into pages at its <see cref="SurveyDefinition.PageBreaks"/>. The API uses it to
    /// clean the breaks a builder sends, and the Blazor app to show one page at a time. The React app
    /// has the same rules in src/logic/pages.ts.
    /// </summary>
    public static class SurveyPaging
    {
        /// <summary>
        /// The page breaks that start a page: questions in the survey other than the first, each once,
        /// in survey order. Anything else is dropped.
        /// </summary>
        public static List<Guid> Normalize(IReadOnlyList<Guid> questionIds, IEnumerable<Guid>? pageBreaks)
        {
            var breaks = (pageBreaks ?? []).ToHashSet();
            return questionIds.Skip(1).Where(breaks.Contains).Distinct().ToList();
        }

        /// <summary>
        /// The survey's questions, page by page. A survey without page breaks, or without questions,
        /// is a single page.
        /// </summary>
        public static List<List<QuestionDefinition>> Split(IEnumerable<QuestionDefinition> questions, IEnumerable<Guid>? pageBreaks)
        {
            var breaks = (pageBreaks ?? []).ToHashSet();
            var pages = new List<List<QuestionDefinition>> { new() };
            foreach (var question in questions)
            {
                if (pages[^1].Count > 0 && breaks.Contains(question.Id))
                {
                    pages.Add([]);
                }
                pages[^1].Add(question);
            }
            return pages;
        }

        /// <summary>
        /// The indexes of the pages a respondent sees: those with at least one visible question for the
        /// answers so far. A page whose questions are all hidden is skipped. Always at least one page.
        /// </summary>
        public static List<int> ShownPages(IReadOnlyList<List<QuestionDefinition>> pages, IReadOnlySet<string> visibleKeys)
        {
            var shown = Enumerable.Range(0, pages.Count)
                .Where(i => pages[i].Any(q => visibleKeys.Contains(q.Key)))
                .ToList();
            return shown.Count > 0 ? shown : [0];
        }

        /// <summary>The errors (keyed by question key) that belong to questions on the page.</summary>
        public static Dictionary<string, string[]> ErrorsOnPage(IEnumerable<QuestionDefinition> page, IReadOnlyDictionary<string, string[]> errors) =>
            page.Where(q => errors.ContainsKey(q.Key)).ToDictionary(q => q.Key, q => errors[q.Key]);

        /// <summary>The index of the first page with an error, or null when none of the errors belong to a page.</summary>
        public static int? FirstPageWithError(IReadOnlyList<List<QuestionDefinition>> pages, IReadOnlyDictionary<string, string[]> errors)
        {
            for (var i = 0; i < pages.Count; i++)
            {
                if (pages[i].Any(q => errors.ContainsKey(q.Key)))
                {
                    return i;
                }
            }
            return null;
        }
    }
}
