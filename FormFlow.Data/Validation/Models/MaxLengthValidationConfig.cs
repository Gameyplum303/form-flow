namespace FormFlow.Data.Validation.Models;

public class MaxLengthValidationConfig
{
    public string ValidationType { get; set; } = ValidationTypes.MaxLength;
    public int MaxLength { get; set; }

    public string? Message { get; set; }
}
