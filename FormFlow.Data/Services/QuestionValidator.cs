using System.Text;
using System.Text.Json;
using FormFlow.Data.Models;

namespace FormFlow.Data.Services
{
    /// <summary>
    /// Provides detailed error information for API responses
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

        private static readonly HashSet<string> KnownRuleTypes =
            ["MinLength", "MaxLength", "MinValue", "MaxValue", "Range"];

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

                    if (type is null || !KnownRuleTypes.Contains(type))
                    {
                        AddError(result, "validationConfigs", "enum",
                            $"Each validation rule needs a validationType of: {string.Join(", ", KnownRuleTypes)}");
                        return;
                    }
                }
            }
            catch (JsonException)
            {
                AddError(result, "validationConfigs", "format", "Validation rules must be valid JSON");
            }
        }

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
        /// <summary>
        /// Gets a human-readable error summary
        /// </summary>
        public string GetErrorSummary(ValidationResult result)
        {
            if (result.Valid)
                return "Question is valid";

            var sb = new StringBuilder("Validation failed:\n");
            foreach (var error in result.Errors)
            {
                sb.AppendLine($"  • {error.Field}: {error.Message}");
            }

            return sb.ToString();
        }
    }
}
