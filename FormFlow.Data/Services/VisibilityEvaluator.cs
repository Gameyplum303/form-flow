using FormFlow.Data.Models;

namespace FormFlow.Data.Services
{
    /// <summary>
    /// Decides which questions are shown, based on each question's <see cref="VisibleIf"/> rule
    /// and the answers given so far. Used by the API when validating a submission and by
    /// clients when rendering a form, so both agree on what the respondent saw.
    /// </summary>
    public static class VisibilityEvaluator
    {
        /// <summary>
        /// Returns the keys of the questions that are visible for the given answers.
        /// A question is hidden when its controlling question is hidden, unanswered, or
        /// answered with a value other than <see cref="VisibleIf.ShouldEqual"/>.
        /// </summary>
        public static HashSet<string> VisibleKeys(
            IEnumerable<QuestionDefinition> questions,
            IReadOnlyDictionary<string, List<string>> answers)
        {
            var byKey = new Dictionary<string, QuestionDefinition>(StringComparer.Ordinal);
            foreach (var question in questions)
            {
                byKey.TryAdd(question.Key, question);
            }

            var visible = new HashSet<string>(StringComparer.Ordinal);
            foreach (var key in byKey.Keys)
            {
                if (IsVisible(key, byKey, answers, new HashSet<string>(StringComparer.Ordinal)))
                {
                    visible.Add(key);
                }
            }
            return visible;
        }

        private static bool IsVisible(
            string key,
            IReadOnlyDictionary<string, QuestionDefinition> byKey,
            IReadOnlyDictionary<string, List<string>> answers,
            HashSet<string> visiting)
        {
            var rule = byKey[key].VisibleIf;
            if (rule is null || string.IsNullOrWhiteSpace(rule.Key))
            {
                return true;
            }

            // A rule pointing at a question outside this survey can never be satisfied,
            // and a cycle of rules would never resolve, so both hide the question.
            if (!byKey.ContainsKey(rule.Key) || !visiting.Add(key))
            {
                return false;
            }

            if (!IsVisible(rule.Key, byKey, answers, visiting))
            {
                return false;
            }

            return TryReadBool(answers, rule.Key, out var answer) && answer == rule.ShouldEqual;
        }

        /// <summary>
        /// Reads a yes/no style answer. Accepts "true"/"false" and "yes"/"no" in any case.
        /// </summary>
        public static bool TryReadBool(IReadOnlyDictionary<string, List<string>> answers, string key, out bool value)
        {
            value = false;
            if (!answers.TryGetValue(key, out var values) || values.Count == 0)
            {
                return false;
            }
            return TryParseBool(values[0], out value);
        }

        public static bool TryParseBool(string? raw, out bool value)
        {
            switch (raw?.Trim().ToLowerInvariant())
            {
                case "true":
                case "yes":
                    value = true;
                    return true;
                case "false":
                case "no":
                    value = false;
                    return true;
                default:
                    value = false;
                    return false;
            }
        }
    }
}
