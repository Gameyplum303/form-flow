namespace FormFlow.Data.Validation.Models;

public class MaxValueValidationConfig
{
    public string ValidationType { get; set; } = ValidationTypes.MaxValue;
    public decimal MaxValue { get; set; }

    public string? Message { get; set; }
}
