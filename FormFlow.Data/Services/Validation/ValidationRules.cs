using System.Text.Json;
using FormFlow.Data.Validation;

namespace FormFlow.Data.Services;

/// <summary>
/// Reads single values out of a question's validation rules JSON.
/// </summary>
public static class ValidationRules
{
    /// <summary>The question's maximum value (from a MaxValue or Range rule), or null when it has none.</summary>
    public static decimal? MaxValue(string? validationConfigs)
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
                    && type.GetString() is ValidationTypes.MaxValue or ValidationTypes.Range
                    && rule.TryGetProperty("maxValue", out var max)
                    && max.TryGetDecimal(out var value))
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
