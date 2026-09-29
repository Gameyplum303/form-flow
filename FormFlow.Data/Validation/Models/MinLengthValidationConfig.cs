namespace FormFlow.Data.Validation.Models;

public class MinLengthValidationConfig
{
    public string ValidationType { get; set; } = ValidationTypes.MinLength;
    public int MinLength { get; set; }

    public string? Message { get; set; }

}
