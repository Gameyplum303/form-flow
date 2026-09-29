using System.Text.Json;
using FormFlow.Data.Validation;

namespace FormFlow.Data.Services;

/// <summary>
/// Reads single values out of a question's validation rules JSON.
/// </summary>
public static class ValidationRules
{
    /// <summary>The question's maximum value (from a MaxValue or Range rule), or null when it has none.</summary>
    public static decimal? MaxValue(string? validationConfigs) =>
        Find(validationConfigs, ValidationTypes.MaxValue, "maxValue");

    /// <summary>The question's minimum value (from a MinValue or Range rule), or null when it has none.</summary>
    public static decimal? MinValue(string? validationConfigs) =>
        Find(validationConfigs, ValidationTypes.MinValue, "minValue");

    private static decimal? Find(string? validationConfigs, string ruleType, string property)
    {
        if (string.IsNullOrWhiteSpace(validationConfigs))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(validationConfigs);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            foreach (var rule in doc.RootElement.EnumerateArray())
            {
                if (rule.ValueKind == JsonValueKind.Object
                    && rule.TryGetProperty("validationType", out var type)
                    && type.ValueKind == JsonValueKind.String
                    && (type.GetString() == ruleType || type.GetString() == ValidationTypes.Range)
                    && rule.TryGetProperty(property, out var limit)
                    && limit.ValueKind == JsonValueKind.Number
                    && limit.TryGetDecimal(out var value))
                {
                    return value;
                }
            }
        }
        catch (JsonException)
        {
        }

        return null;
    }
}
