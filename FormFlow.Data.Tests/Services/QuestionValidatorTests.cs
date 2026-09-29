using FluentAssertions;
using FormFlow.Data.Models;
using FormFlow.Data.Services;
using Xunit;

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
        Assert.Contains(result.Errors, e => e.Message != null && e.Message.Contains("empty"));
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

    [Theory]
    [InlineData("""[{"validationType":"MaxValue"}]""", "maxValue as a number")]
    [InlineData("""[{"validationType":"MaxValue","maxValue":"10"}]""", "maxValue as a number")]
    [InlineData("""[{"validationType":"MinValue","minValue":null}]""", "minValue as a number")]
    [InlineData("""[{"validationType":"Range","minValue":1}]""", "maxValue as a number")]
    [InlineData("""[{"validationType":"MinLength","minLength":2.5}]""", "minLength as a whole number")]
    [InlineData("""[{"validationType":"MaxLength","maxLength":-1}]""", "maxLength as a whole number")]
    [InlineData("""[{"validationType":"MaxLength","maxLength":10,"message":5}]""", "message must be text")]
    public void ValidationRules_WithUnreadableValues_AreRejected(string rules, string expected)
    {
        var result = _validator.Validate(Q(q => q.ValidationConfigs = rules));

        result.Errors.Should().ContainSingle().Which.Message.Should().Contain(expected);
    }

    [Theory]
    [InlineData("""[{"validationType":"MaxValue","maxValue":7.5}]""")]
    [InlineData("""[{"validationType":"Range","minValue":-0.5,"maxValue":7.5,"message":"Between -0.5 and 7.5"}]""")]
    [InlineData("""[{"validationType":"MinLength","MinLength":2},{"validationType":"MaxLength","maxLength":10,"message":null}]""")]
    public void ValidationRules_WithNumbers_AreAccepted_AndCheckAnswersWithoutThrowing(string rules)
    {
        _validator.Validate(Q(q => q.ValidationConfigs = rules)).Valid.Should().BeTrue();

        var check = () => new QuestionValidationEngine().Validate("5", rules, out _);
        check.Should().NotThrow();
    }

    [Fact]
    public void QuestionCannotDependOnItself()
    {
        var result = _validator.Validate(Q(q => q.VisibleIf = new VisibleIf { Key = "k", ShouldEqual = true }));

        result.Valid.Should().BeFalse();
    }
    [Theory]
    [InlineData("long_text")]
    [InlineData("email")]
    [InlineData("date")]
    [InlineData("rating")]
    public void NewQuestionTypes_AreAcceptedWithoutOptions(string type)
    {
        _validator.Validate(Q(q => q.Type = type)).Valid.Should().BeTrue();
    }

    [Theory]
    [InlineData(2, true)]
    [InlineData(10, true)]
    [InlineData(1, false)]
    [InlineData(11, false)]
    public void RatingScale_MustBeTwoToTenStars(int stars, bool valid)
    {
        var result = _validator.Validate(Q(q =>
        {
            q.Type = "rating";
            q.ValidationConfigs = $$"""[{"validationType":"MaxValue","maxValue":{{stars}}}]""";
        }));

        result.Valid.Should().Be(valid);
    }

    [Theory]
    [InlineData(null, 5)]
    [InlineData("""[{"validationType":"MaxValue","maxValue":7}]""", 7)]
    [InlineData("""[{"validationType":"Range","minValue":1,"maxValue":3}]""", 3)]
    [InlineData("""[{"validationType":"MaxValue","maxValue":50}]""", 5)]
    [InlineData("not json", 5)]
    public void RatingScale_ComesFromTheMaximumValue(string? rules, int expected)
    {
        QuestionTypes.RatingScale(Q(q => { q.Type = "rating"; q.ValidationConfigs = rules; })).Should().Be(expected);
    }
}
