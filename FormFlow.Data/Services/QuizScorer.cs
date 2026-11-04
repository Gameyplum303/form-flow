using System.Globalization;
using FormFlow.Data.Models;

namespace FormFlow.Data.Services
{
    /// <summary>
    /// Grades answers against the questions' answer keys. It is pure: the API grades a submission with
    /// it, and the results page uses it to count how often each question was answered correctly.
    /// </summary>
    /// <remarks>
    /// <para>Only visible questions with an <see cref="AnswerKey"/> are graded; hidden questions and questions
    /// without a key don't count toward the maximum. An unanswered graded question earns nothing.</para>
    /// <para>Radio, dropdown and yes/no need the one correct option, number the exact value (so 3 and 3.0 are
    /// the same), and text any accepted answer, ignoring case and spaces at either end.</para>
    /// <para>Checkbox and multiselect are correct only for the exact set of options, but earn partial credit:
    /// each correct option ticked earns its share of the points, each wrong option ticked takes a share
    /// away, and a question never scores below zero. With two correct options out of four, ticking one
    /// of them earns half, and ticking all four earns nothing.</para>
    /// </remarks>
    public static class QuizScorer
    {
        /// <summary>
        /// Grades a set of answers, as <see cref="ResponseValidator"/> normalized them, to a survey's
        /// questions. The result lists every graded question in survey order.
        /// </summary>
        public static QuizResult Score(IReadOnlyList<QuestionDefinition> questions, IReadOnlyDictionary<string, List<string>> answers)
        {
            var visible = VisibilityEvaluator.VisibleKeys(questions, answers);
            var grades = questions
                .Where(q => visible.Contains(q.Key) && IsGraded(q))
                .Select(q => Grade(q, answers.TryGetValue(q.Key, out var values) ? values : null))
                .ToList();

            var score = Round(grades.Sum(g => g.Points));
            var max = grades.Sum(g => g.MaxPoints);
            return new QuizResult
            {
                Score = score,
                MaxScore = max,
                Percent = Percent(score, max),
                Questions = grades,
            };
        }

        /// <summary>Whether a question counts in a quiz: a gradable type with an answer key that has a correct answer.</summary>
        public static bool IsGraded(QuestionDefinition question) =>
            question.AnswerKey is { CorrectAnswers.Count: > 0 } && QuestionTypes.IsGradable(question);

        /// <summary>Grades one question. <paramref name="answer"/> is null or empty when it wasn't answered.</summary>
        public static QuestionGrade Grade(QuestionDefinition question, IReadOnlyList<string>? answer)
        {
            var key = question.AnswerKey ?? new AnswerKey();
            var given = answer ?? [];
            var correct = key.CorrectAnswers;
            var type = question.Type.ToLowerInvariant();

            var share = type is QuestionTypes.Checkbox or QuestionTypes.Multiselect
                ? SetShare(correct, given)
                : IsRight(type, correct, given) ? 1m : 0m;

            return new QuestionGrade
            {
                Key = question.Key,
                Label = question.Label,
                Points = Round(key.Points * share),
                MaxPoints = key.Points,
                Correct = share == 1m,
                GivenAnswers = given.Select(v => Display(question, v)).ToList(),
                CorrectAnswers = correct.Select(v => Display(question, type == QuestionTypes.Text ? v.Trim() : v)).ToList(),
                Explanation = string.IsNullOrWhiteSpace(key.Explanation) ? null : key.Explanation.Trim(),
            };
        }

        /// <summary>A score as a whole-number percentage of the maximum, rounding halves up; 0 when the maximum is 0.</summary>
        public static int Percent(decimal score, decimal max) =>
            max <= 0 ? 0 : (int)Math.Round(score * 100 / max, MidpointRounding.AwayFromZero);

        private static bool IsRight(string type, List<string> correct, IReadOnlyList<string> given)
        {
            if (given.Count != 1)
            {
                return false;
            }
            var value = given[0];
            return type switch
            {
                QuestionTypes.YesNo => VisibilityEvaluator.TryParseBool(value, out var answer)
                    && VisibilityEvaluator.TryParseBool(correct[0], out var expected) && answer == expected,
                QuestionTypes.Number => TryNumber(value, out var number)
                    && TryNumber(correct[0], out var expectedNumber) && number == expectedNumber,
                QuestionTypes.Text => correct.Any(c => string.Equals(c.Trim(), value.Trim(), StringComparison.OrdinalIgnoreCase)),
                _ => string.Equals(value, correct[0], StringComparison.Ordinal),
            };
        }

        /// <summary>
        /// The share of the points a checkbox or multiselect answer earns: correct options ticked minus
        /// wrong options ticked, out of the number of correct options, and never below zero.
        /// </summary>
        private static decimal SetShare(List<string> correct, IReadOnlyList<string> given)
        {
            var expected = correct.ToHashSet(StringComparer.Ordinal);
            var chosen = given.ToHashSet(StringComparer.Ordinal);
            if (expected.Count == 0)
            {
                return 0m;
            }
            if (chosen.SetEquals(expected))
            {
                return 1m;
            }
            var right = chosen.Count(expected.Contains);
            var wrong = chosen.Count - right;
            return Math.Max(0, right - wrong) / (decimal)expected.Count;
        }

        /// <summary>An answer value as respondents see it: an option's label, or Yes or No.</summary>
        private static string Display(QuestionDefinition question, string value)
        {
            if (string.Equals(question.Type, QuestionTypes.YesNo, StringComparison.OrdinalIgnoreCase))
            {
                return VisibilityEvaluator.TryParseBool(value, out var yes) ? (yes ? "Yes" : "No") : value;
            }
            return question.Options.FirstOrDefault(o => o.Value == value)?.Label ?? value;
        }

        private static bool TryNumber(string value, out decimal number) =>
            decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out number);

        private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
    }
}
