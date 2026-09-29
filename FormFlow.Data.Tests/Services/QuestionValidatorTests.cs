using FluentAssertions;
using Xunit;
using FormFlow.Data.Models;
using FormFlow.Data.Services;

namespace FormFlow.Tests;

public class QuestionValidatorTests
{
    private readonly QuestionValidator _validator = new();

    [Fact]
    public void Validate_NullQuestion_ReturnsInvalid()
    {
        var result = _validator.Validate(null!);

        Assert.False(result.Valid);
        Assert.Single(result.Errors);
        Assert.Equal("Question object cannot be null", result.Errors[0].Message);
    }

    [Fact]
    public void Validate_EmptyJsonString_Invalid()
    {
        var result = _validator.ValidateJson(string.Empty);
        Assert.False(result.Valid);
        Assert.Contains(result.Errors, e => e.Message.Contains("empty"));
    }
}

public class QuestionValidatorRuleTests
{
    private readonly QuestionValidator _validator = new();

    private static QuestionDefinition Q(Action<QuestionDefinition>? change = null)
    {
        var q = new QuestionDefinition { Id = Guid.NewGuid(), Key = "k", Label = "L", Type = "text" };
        change?.Invoke(q);
        return q;
    }

    [Fact]
    public void CheckboxWithoutOptions_IsValid()
    {
        _validator.Validate(Q(q => q.Type = "checkbox")).Valid.Should().BeTrue();
    }

    [Fact]
    public void DuplicateOptionValues_AreRejected()
    {
        var result = _validator.Validate(Q(q =>
        {
            q.Type = "radio";
            q.Options = [new Option { Label = "A", Value = "x" }, new Option { Label = "B", Value = "x" }];
        }));

        result.Errors.Should().Contain(e => e.Message == "Option values must be unique");
    }

    [Theory]
    [InlineData("{}", "JSON array")]
    [InlineData("[{\"validationType\":\"Nope\"}]", "validationType")]
    [InlineData("[{", "valid JSON")]
    public void MalformedValidationRules_AreRejected(string rules, string expected)
    {
        var result = _validator.Validate(Q(q => q.ValidationConfigs = rules));

        result.Errors.Should().Contain(e => e.Message!.Contains(expected));
    }

    [Fact]
    public void QuestionCannotDependOnItself()
    {
        var result = _validator.Validate(Q(q => q.VisibleIf = new VisibleIf { Key = "k", ShouldEqual = true }));

        result.Valid.Should().BeFalse();
    }
}
