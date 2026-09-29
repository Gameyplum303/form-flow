using FluentAssertions;
using FormFlow.Data.Models;
using FormFlow.Data.Services;

namespace FormFlow.Data.Tests.Services;

public class ResponseValidatorTests
{
    private readonly ResponseValidator _validator = new(new QuestionValidationEngine());

    private static QuestionDefinition Q(string key, string type, bool required = true, params string[] options) => new()
    {
        Id = Guid.NewGuid(),
        Key = key,
        Label = key,
        Type = type,
        Required = required,
        Options = options.Select(o => new Option { Label = o, Value = o }).ToList()
    };

    private static Dictionary<string, List<string>> Answers(params (string Key, string[] Values)[] answers) =>
        answers.ToDictionary(a => a.Key, a => a.Values.ToList());

    [Fact]
    public void OptionalQuestionLeftBlank_IsValidAndNotStored()
    {
        var result = _validator.Validate([Q("note", "text", required: false)], Answers(("note", ["  "])));

        result.IsValid.Should().BeTrue();
        result.Answers.Should().BeEmpty();
    }

    [Fact]
    public void SingleValueQuestion_RejectsMultipleValues()
    {
        var result = _validator.Validate([Q("color", "radio", true, "red", "blue")], Answers(("color", ["red", "blue"])));

        result.Errors["color"].Should().Contain("Only one answer is allowed.");
    }

    [Fact]
    public void Multiselect_RejectsDuplicateSelections()
    {
        var result = _validator.Validate([Q("skills", "multiselect", true, "a", "b")], Answers(("skills", ["a", "a"])));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void YesNo_IsNormalizedToTrueOrFalse()
    {
        var result = _validator.Validate([Q("ok", "yes_no")], Answers(("ok", ["Yes"])));

        result.Answers["ok"].Should().Equal("true");
    }

    [Fact]
    public void CheckboxWithoutOptions_IsATickBox()
    {
        var result = _validator.Validate([Q("agree", "checkbox")], Answers(("agree", ["true"])));

        result.IsValid.Should().BeTrue();
        result.Answers["agree"].Should().Equal("true");
    }

    [Fact]
    public void TextRules_AreApplied()
    {
        var question = Q("bio", "text");
        question.ValidationConfigs = """[{"validationType":"MaxLength","maxLength":3,"message":"Too long"}]""";

        var result = _validator.Validate([question], Answers(("bio", ["abcd"])));

        result.Errors["bio"].Should().Equal("Too long");
    }

    [Fact]
    public void DecimalNumbers_AreAccepted()
    {
        var question = Q("gpa", "number");
        question.ValidationConfigs = """[{"validationType":"Range","minValue":0,"maxValue":4}]""";

        _validator.Validate([question], Answers(("gpa", ["3.7"]))).IsValid.Should().BeTrue();
        _validator.Validate([question], Answers(("gpa", ["4.5"]))).IsValid.Should().BeFalse();
    }
    [Theory]
    [InlineData("ada@example.com", true)]
    [InlineData("first.last+tag@uni.edu", true)]
    [InlineData("not-an-email", false)]
    [InlineData("ada@example", false)]
    [InlineData("two@@example.com", false)]
    public void Email_MustLookLikeAnAddress(string answer, bool valid)
    {
        var result = _validator.Validate([Q("email", "email")], Answers(("email", [answer])));

        result.IsValid.Should().Be(valid);
        if (!valid)
        {
            result.Errors["email"].Should().Equal("Answer must be an email address, like name@example.com.");
        }
    }

    [Theory]
    [InlineData("2026-09-29", true)]
    [InlineData("2024-02-29", true)]
    [InlineData("2026-02-30", false)]
    [InlineData("09/29/2026", false)]
    [InlineData("yesterday", false)]
    public void Date_MustBeAnIsoDate(string answer, bool valid)
    {
        var result = _validator.Validate([Q("start", "date")], Answers(("start", [answer])));

        result.IsValid.Should().Be(valid);
        if (valid)
        {
            result.Answers["start"].Should().Equal(answer);
        }
    }

    [Theory]
    [InlineData("1", true)]
    [InlineData("5", true)]
    [InlineData("0", false)]
    [InlineData("6", false)]
    [InlineData("4.5", false)]
    [InlineData("-1", false)]
    public void Rating_DefaultsToOneToFiveStars(string answer, bool valid)
    {
        var result = _validator.Validate([Q("stars", "rating")], Answers(("stars", [answer])));

        result.IsValid.Should().Be(valid);
        if (!valid)
        {
            result.Errors["stars"].Should().Equal("Answer must be a whole number from 1 to 5.");
        }
    }

    [Fact]
    public void Rating_UsesTheQuestionsMaximumValueAsItsScale()
    {
        var question = Q("stars", "rating");
        question.ValidationConfigs = """[{"validationType":"MaxValue","maxValue":10}]""";

        _validator.Validate([question], Answers(("stars", ["10"]))).IsValid.Should().BeTrue();
        _validator.Validate([question], Answers(("stars", ["11"]))).Errors["stars"]
            .Should().Equal("Answer must be a whole number from 1 to 10.");
    }

    [Fact]
    public void LongText_UsesLengthRules()
    {
        var question = Q("comments", "long_text");
        question.ValidationConfigs = """[{"validationType":"MaxLength","maxLength":10}]""";

        _validator.Validate([question], Answers(("comments", ["Short one."]))).IsValid.Should().BeTrue();
        _validator.Validate([question], Answers(("comments", ["This answer is too long."]))).Errors["comments"]
            .Should().Equal("Maximum length is 10.");
    }
}
