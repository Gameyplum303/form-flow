using System.Text.Json;
using FormFlow.Data.Models;
using FormFlow.Data.Validation;

namespace FormFlow.Data.Services
{
    /// <summary>
    /// Checks a question before it is saved: its required fields, type, options, visibility rule and
    /// validation rules. Each problem becomes a <see cref="ValidationError"/> the API can return.
    /// </summary>
    public class QuestionValidator
    {
        /// <summary>
        /// Validation result containing success status and any errors
        /// </summary>
        public class ValidationResult
        {
            public bool Valid { get; set; }
            public List<ValidationError> Errors { get; set; } = new();

            public ValidationResult(bool valid = true)
            {
                Valid = valid;
            }
        }

        /// <summary>
        /// Represents a single validation error
        /// </summary>
        public class ValidationError
        {
            public string? Field { get; set; }
            public string? Property { get; set; }
            public string? Message { get; set; }
        }

        /// <summary>
        /// Validates a question object
        /// </summary>
        /// <param name="question">The question to validate</param>
        /// <returns>ValidationResult with details about any validation failures</returns>
        public ValidationResult Validate(QuestionDefinition question)
        {
            var result = new ValidationResult(true);
            if (question == null)
            {
                result.Valid = false;
                result.Errors.Add(new ValidationError
                {
                    Field = "root",
                    Property = "null",
                    Message = "Question object cannot be null"
                });
                return result;
            }

            if (string.IsNullOrWhiteSpace(question.Key))
            {
                result.Valid = false;
                result.Errors.Add(new ValidationError
                {
                    Field = "key",
                    Property = "required",
                    Message = "Question key is required and cannot be empty"
                });
            }
            if (string.IsNullOrWhiteSpace(question.Label))
            {
                result.Valid = false;
                result.Errors.Add(new ValidationError
                {
                    Field = "label",
                    Property = "required",
                    Message = "Question label is required and cannot be empty"
                });
            }
            if (string.IsNullOrWhiteSpace(question.Type))
            {
                result.Valid = false;
                result.Errors.Add(new ValidationError
                {
                    Field = "type",
                    Property = "required",
                    Message = "Question type is required and cannot be empty"
                });
            }
            else if (!QuestionTypes.IsKnown(question.Type))
            {
                AddError(result, "type", "enum", $"Question type must be one of: {string.Join(", ", QuestionTypes.All)}");
            }
            else
            {
                ValidateOptions(question, result);
                if (string.Equals(question.Type, QuestionTypes.Rating, StringComparison.OrdinalIgnoreCase)
                    && ValidationRules.MaxValue(question.ValidationConfigs) is { } scale
                    && (scale < 2 || scale > QuestionTypes.MaxRatingScale || scale != Math.Floor(scale)))
                {
                    AddError(result, "validationConfigs", "range",
                        $"A rating needs a whole number of stars from 2 to {QuestionTypes.MaxRatingScale}");
                }
            }

            if (question.VisibleIf is not null)
            {
                if (string.IsNullOrWhiteSpace(question.VisibleIf.Key))
                {
                    AddError(result, "visibleIf.key", "required", "Visibility rule must name the question it depends on");
                }
                else if (question.VisibleIf.Key == question.Key)
                {
                    AddError(result, "visibleIf.key", "self", "A question cannot depend on itself");
                }
            }

            ValidateRules(question.ValidationConfigs, result);

            return result;

        }

        private static void ValidateOptions(QuestionDefinition question, ValidationResult result)
        {
            var options = question.Options ?? [];

            // A checkbox with no options is a single tick box; the other choice types need options.
            var needsOptions = QuestionTypes.IsChoice(question.Type)
                && !string.Equals(question.Type, QuestionTypes.Checkbox, StringComparison.OrdinalIgnoreCase);
            if (needsOptions && options.Count == 0)
            {
                AddError(result, "options", "required", $"Question type '{question.Type}' requires at least one option");
                return;
            }

            if (options.Any(o => string.IsNullOrWhiteSpace(o.Label) || string.IsNullOrWhiteSpace(o.Value)))
            {
                AddError(result, "options", "required", "Each option needs a label and a value");
            }
            else if (options.Select(o => o.Value).Distinct(StringComparer.Ordinal).Count() != options.Count)
            {
                AddError(result, "options", "unique", "Option values must be unique");
            }
        }

        // The numbers each rule type needs. Lengths count characters, so they are whole numbers;
        // values can have decimals.
        private static readonly Dictionary<string, (string Field, bool WholeNumber)[]> RuleFields = new()
        {
            [ValidationTypes.MinLength] = [("minLength", true)],
            [ValidationTypes.MaxLength] = [("maxLength", true)],
            [ValidationTypes.MinValue] = [("minValue", false)],
            [ValidationTypes.MaxValue] = [("maxValue", false)],
            [ValidationTypes.Range] = [("minValue", false), ("maxValue", false)],
        };

        private static void ValidateRules(string? validationConfigs, ValidationResult result)
        {
            if (string.IsNullOrWhiteSpace(validationConfigs))
            {
                return;
            }

            try
            {
                using var doc = JsonDocument.Parse(validationConfigs);
                if (doc.RootElement.ValueKind != JsonValueKind.Array)
                {
                    AddError(result, "validationConfigs", "format", "Validation rules must be a JSON array");
                    return;
                }

                foreach (var rule in doc.RootElement.EnumerateArray())
                {
                    var type = rule.ValueKind == JsonValueKind.Object
                        && rule.TryGetProperty("validationType", out var typeElement)
                        && typeElement.ValueKind == JsonValueKind.String
                            ? typeElement.GetString()
                            : null;

                    if (type is null || !RuleFields.TryGetValue(type, out var fields))
                    {
                        AddError(result, "validationConfigs", "enum",
                            $"Each validation rule needs a validationType of: {string.Join(", ", RuleFields.Keys)}");
                        return;
                    }

                    // Answers are checked against these numbers later, so a rule that can't be read
                    // is turned away now rather than failing every submission.
                    foreach (var (field, wholeNumber) in fields)
                    {
                        var values = Properties(rule, field).ToList();
                        if (values.Count == 0 || !values.All(v => wholeNumber ? IsCount(v) : IsNumber(v)))
                        {
                            AddError(result, "validationConfigs", "number", wholeNumber
                                ? $"A {type} rule needs {field} as a whole number, 0 or more"
                                : $"A {type} rule needs {field} as a number");
                            return;
                        }
                    }

                    if (Properties(rule, "message").Any(m => m.ValueKind is not (JsonValueKind.String or JsonValueKind.Null)))
                    {
                        AddError(result, "validationConfigs", "format", "A validation rule's message must be text");
                        return;
                    }
                }
            }
            catch (JsonException)
            {
                AddError(result, "validationConfigs", "format", "Validation rules must be valid JSON");
            }
        }

        // Rules are read ignoring the case of property names, as QuestionValidationEngine reads them.
        private static IEnumerable<JsonElement> Properties(JsonElement rule, string name) =>
            rule.EnumerateObject()
                .Where(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))
                .Select(p => p.Value);

        private static bool IsNumber(JsonElement value) =>
            value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out _);

        private static bool IsCount(JsonElement value) =>
            value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var count) && count >= 0;

        private static void AddError(ValidationResult result, string field, string property, string message)
        {
            result.Valid = false;
            result.Errors.Add(new ValidationError { Field = field, Property = property, Message = message });
        }

        /// <summary>
        /// Validates a JSON string directly
        /// </summary>
        public ValidationResult ValidateJson(string jsonData)
        {
            if (string.IsNullOrWhiteSpace(jsonData))
            {
                var result = new ValidationResult(false);
                result.Errors.Add(new ValidationError
                {
                    Field = "root",
                    Property = "empty",
                    Message = "Question JSON cannot be empty"
                });
                return result;
            }

            try
            {
                var question = JsonSerializer.Deserialize<QuestionDefinition>(jsonData);
                if (question == null)
                {
                    var result = new ValidationResult(false);
                    result.Errors.Add(new ValidationError
                    {
                        Field = "root",
                        Property = "null",
                        Message = "Deserialized question is null"
                    });
                    return result;
                }
                return Validate(question);

            }
            catch (JsonException ex)
            {
                var result = new ValidationResult(false);
                result.Errors.Add(new ValidationError
                {
                    Field = "root",
                    Property = "json_parse",
                    Message = $"Invalid JSON: {ex.Message}"
                });
                return result;
            }
            catch (Exception ex)
            {
                var result = new ValidationResult(false);
                result.Errors.Add(new ValidationError
                {
                    Field = "root",
                    Property = "exception",
                    Message = $"Validation service error: {ex.Message}"
                });
                return result;
            }
        }
    }
}
