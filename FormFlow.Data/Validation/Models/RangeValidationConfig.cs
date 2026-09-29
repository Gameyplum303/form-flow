namespace FormFlow.Data.Validation.Models;

public class RangeValidationConfig
{
    public string ValidationType { get; set; } = ValidationTypes.Range;
    public decimal MinValue { get; set; }
    public decimal MaxValue { get; set; }
    public string? Message { get; set; }
}
