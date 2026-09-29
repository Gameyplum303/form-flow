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
}
