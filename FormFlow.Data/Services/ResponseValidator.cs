using System.Globalization;
using System.Text.RegularExpressions;
using FormFlow.Data.Models;

namespace FormFlow.Data.Services
{
    /// <summary>
    /// Outcome of validating a survey submission.
    /// </summary>
    public class ResponseValidationResult
    {
        public bool IsValid => Errors.Count == 0;

        /// <summary>Error messages keyed by question key.</summary>
        public Dictionary<string, string[]> Errors { get; } = new();

        /// <summary>
        /// The answers to store: only visible questions, trimmed, with yes/no answers
        /// normalized to "true"/"false" and likert rows in row order. Only meaningful when <see cref="IsValid"/> is true.
        /// </summary>
        public Dictionary<string, List<string>> Answers { get; } = new();
    }

    /// <summary>
    /// Validates a respondent's answers against the survey's questions: required answers (for a
    /// likert grid, every statement), answer types, allowed options and ranges, conditional visibility, and each question's
    /// <see cref="QuestionDefinition.ValidationConfigs"/> rules.
    /// </summary>
    public class ResponseValidator
    {
        private readonly QuestionValidationEngine _engine;

        public ResponseValidator(QuestionValidationEngine engine)
        {
            _engine = engine;
        }

        public ResponseValidationResult Validate(
            IReadOnlyList<QuestionDefinition> questions,
            IReadOnlyDictionary<string, List<string>> submitted)
        {
            var result = new ResponseValidationResult();
            var cleaned = Clean(submitted);

            var knownKeys = questions.Select(q => q.Key).ToHashSet(StringComparer.Ordinal);
            foreach (var key in cleaned.Keys.Where(k => !knownKeys.Contains(k)))
            {
                result.Errors[key] = ["This question is not part of the survey."];
            }

            var visible = VisibilityEvaluator.VisibleKeys(questions, cleaned);

            foreach (var question in questions)
            {
                // Answers to hidden questions are dropped rather than rejected, so a client
                // that keeps stale values after a question disappears still submits cleanly.
                if (!visible.Contains(question.Key))
                {
                    continue;
                }

                cleaned.TryGetValue(question.Key, out var values);
                values ??= [];

                if (values.Count == 0)
                {
                    if (question.Required)
                    {
                        result.Errors[question.Key] = ["This question is required."];
                    }
                    continue;
                }

                var errors = ValidateAnswer(question, values, out var normalized);
                if (errors.Count > 0)
                {
                    result.Errors[question.Key] = errors.ToArray();
                }
                else
                {
                    result.Answers[question.Key] = normalized;
                }
            }

            return result;
        }

        private List<string> ValidateAnswer(QuestionDefinition question, List<string> values, out List<string> normalized)
        {
            var errors = new List<string>();
            normalized = values;
            var type = question.Type.ToLowerInvariant();

            if (!QuestionTypes.IsMultiValue(type) && values.Count > 1)
            {
                errors.Add("Only one answer is allowed.");
                return errors;
            }

            switch (type)
            {
                case QuestionTypes.Text:
                case QuestionTypes.LongText:
                    break;

                case QuestionTypes.Email:
                    if (!IsEmail(values[0]))
                    {
                        errors.Add("Answer must be an email address, like name@example.com.");
                        return errors;
                    }
                    break;

                case QuestionTypes.Date:
                    if (!DateOnly.TryParseExact(values[0], DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                    {
                        errors.Add("Answer must be a date in the form YYYY-MM-DD.");
                        return errors;
                    }
                    normalized = [date.ToString(DateFormat, CultureInfo.InvariantCulture)];
                    break;

                case QuestionTypes.Rating:
                    var scale = QuestionTypes.RatingScale(question);
                    if (!int.TryParse(values[0], NumberStyles.None, CultureInfo.InvariantCulture, out var stars) || stars < 1 || stars > scale)
                    {
                        errors.Add($"Answer must be a whole number from 1 to {scale}.");
                        return errors;
                    }
                    normalized = [stars.ToString(CultureInfo.InvariantCulture)];
                    break;

                case QuestionTypes.Nps:
                    if (!int.TryParse(values[0], NumberStyles.None, CultureInfo.InvariantCulture, out var score) || score > QuestionTypes.NpsMax)
                    {
                        errors.Add($"Answer must be a whole number from 0 to {QuestionTypes.NpsMax}.");
                        return errors;
                    }
                    normalized = [score.ToString(CultureInfo.InvariantCulture)];
                    break;

                case QuestionTypes.Slider:
                    // The slider moves in whole steps from its minimum to its maximum.
                    var (min, max) = QuestionTypes.SliderRange(question);
                    if (!decimal.TryParse(values[0], NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var position)
                        || position != Math.Floor(position) || position < min || position > max)
                    {
                        errors.Add($"Answer must be a whole number from {Format(min)} to {Format(max)}.");
                        return errors;
                    }
                    normalized = [Format(position)];
                    break;

                case QuestionTypes.Likert:
                    normalized = ValidateLikert(question, values, errors);
                    if (errors.Count > 0)
                    {
                        return errors;
                    }
                    break;

                case QuestionTypes.Number:
                    if (!decimal.TryParse(values[0], NumberStyles.Number, CultureInfo.InvariantCulture, out _))
                    {
                        errors.Add("Answer must be a number.");
                        return errors;
                    }
                    break;

                case QuestionTypes.YesNo:
                    if (!VisibilityEvaluator.TryParseBool(values[0], out var answer))
                    {
                        errors.Add("Answer must be yes or no.");
                        return errors;
                    }
                    normalized = [answer ? "true" : "false"];
                    break;

                case QuestionTypes.Checkbox when question.Options.Count == 0:
                    // A checkbox without options is a single tick box.
                    if (!VisibilityEvaluator.TryParseBool(values[0], out var ticked) || values.Count > 1)
                    {
                        errors.Add("Answer must be true or false.");
                        return errors;
                    }
                    normalized = [ticked ? "true" : "false"];
                    break;

                default:
                    var allowed = question.Options.Select(o => o.Value).ToHashSet(StringComparer.Ordinal);
                    var invalid = values.Where(v => !allowed.Contains(v)).ToList();
                    if (invalid.Count > 0)
                    {
                        errors.Add($"'{string.Join("', '", invalid)}' is not one of the options.");
                        return errors;
                    }
                    if (values.Distinct(StringComparer.Ordinal).Count() != values.Count)
                    {
                        errors.Add("Each option can only be selected once.");
                        return errors;
                    }
                    break;
            }

            // Length and value rules only make sense for free-form answers.
            var hasRules = QuestionTypes.HasLengthRules(type) || type == QuestionTypes.Number;
            if (hasRules && !_engine.Validate(values[0], question.ValidationConfigs, out var ruleErrors))
            {
                errors.AddRange(ruleErrors);
            }

            return errors;
        }

        /// <summary>
        /// Checks a likert grid's answers, one "row=option" entry per rated statement, and returns them
        /// in row order. Every statement must be rated when the question is required.
        /// </summary>
        private static List<string> ValidateLikert(QuestionDefinition question, List<string> values, List<string> errors)
        {
            var rows = question.Rows.Select(r => r.Value).ToList();
            var scale = QuestionTypes.LikertScale(question).Select(o => o.Value).ToHashSet(StringComparer.Ordinal);
            var chosen = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (var value in values)
            {
                if (!QuestionTypes.TryParseLikertAnswer(value, out var row, out var option) || !rows.Contains(row))
                {
                    errors.Add($"'{value}' is not an answer to one of the statements.");
                }
                else if (!scale.Contains(option))
                {
                    errors.Add($"'{option}' is not one of the options.");
                }
                else if (!chosen.TryAdd(row, option))
                {
                    errors.Add("Each statement can only have one answer.");
                }
            }

            if (errors.Count == 0 && question.Required && chosen.Count < rows.Count)
            {
                errors.Add("Please answer every statement.");
            }

            var distinct = errors.Distinct().ToList();
            errors.Clear();
            errors.AddRange(distinct);
            return rows.Where(chosen.ContainsKey).Select(r => QuestionTypes.LikertAnswer(r, chosen[r])).ToList();
        }

        private static string Format(decimal value) => value.ToString("0.############", CultureInfo.InvariantCulture);

        /// <summary>Dates are stored and exchanged as ISO dates, the format HTML date inputs use.</summary>
        public const string DateFormat = "yyyy-MM-dd";

        private static readonly Regex EmailPattern = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.CultureInvariant);

        /// <summary>A light check (something@something.something, at most 254 characters), used for answers and sign-ups.</summary>
        public static bool IsEmail(string value) => value.Length <= 254 && EmailPattern.IsMatch(value);

        private static Dictionary<string, List<string>> Clean(IReadOnlyDictionary<string, List<string>> submitted)
        {
            var cleaned = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (var (key, values) in submitted)
            {
                cleaned[key] = (values ?? [])
                    .Where(v => !string.IsNullOrWhiteSpace(v))
                    .Select(v => v.Trim())
                    .ToList();
            }
            return cleaned;
        }
    }
}
