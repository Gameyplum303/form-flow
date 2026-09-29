namespace FormFlow.Data.Validation.Models;

public class MinValueValidationConfig
{
    public string ValidationType { get; set; } = ValidationTypes.MinValue;
    public decimal MinValue { get; set; }
    public string? Message { get; set; }
}
