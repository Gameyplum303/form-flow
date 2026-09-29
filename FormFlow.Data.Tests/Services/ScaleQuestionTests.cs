using FluentAssertions;
using FormFlow.Data.Models;
using FormFlow.Data.Services;

namespace FormFlow.Data.Tests.Services;

/// <summary>Likert grids, NPS and sliders: how their questions and answers are checked.</summary>
public class ScaleQuestionTests
{
    private readonly ResponseValidator _responses = new(new QuestionValidationEngine());
    private readonly QuestionValidator _questions = new();

    private static QuestionDefinition Likert(bool required = true, params string[] scale) => new()
    {
        Id = Guid.NewGuid(),
        Key = "services",
        Label = "How much do you agree?",
        Type = QuestionTypes.Likert,
        Required = required,
        Options = scale.Select(o => new Option { Label = o.ToUpperInvariant(), Value = o }).ToList(),
        Rows =
        [
            new Option { Label = "The library has what I need", Value = "library" },
            new Option { Label = "The labs are up to date", Value = "labs" },
        ]
    };

    private static QuestionDefinition Q(string type, string? rules = null) => new()
    {
        Id = Guid.NewGuid(),
        Key = "q",
        Label = "Question",
        Type = type,
        ValidationConfigs = rules
    };

    private ResponseValidationResult Answer(QuestionDefinition question, params string[] values) =>
        _responses.Validate([question], new Dictionary<string, List<string>> { [question.Key] = values.ToList() });

    [Fact]
    public void Likert_StoresOneAnswerPerRow_InRowOrder()
    {
        var result = Answer(Likert(), "labs=2", "library=5");

        result.IsValid.Should().BeTrue();
        result.Answers["services"].Should().Equal("library=5", "labs=2");
    }

    [Fact]
    public void Likert_WithoutOptions_UsesTheFivePointAgreementScale()
    {
        QuestionTypes.LikertScale(Likert()).Select(o => (o.Value, o.Label)).Should().Equal(
            ("1", "Strongly disagree"), ("2", "Disagree"), ("3", "Neutral"), ("4", "Agree"), ("5", "Strongly agree"));
        Answer(Likert(), "library=6", "labs=1").Errors["services"].Should().Equal("'6' is not one of the options.");
    }

    [Fact]
    public void Likert_WithOptions_UsesThemAsTheScale()
    {
        var question = Likert(true, "never", "often");

        Answer(question, "library=often", "labs=never").IsValid.Should().BeTrue();
        Answer(question, "library=often", "labs=3").Errors["services"].Should().Equal("'3' is not one of the options.");
    }

    [Theory]
    [InlineData("gym=3")]
    [InlineData("library")]
    [InlineData("=3")]
    public void Likert_RejectsUnknownRows(string answer)
    {
        Answer(Likert(), answer, "labs=3").Errors["services"]
            .Should().Equal($"'{answer}' is not an answer to one of the statements.");
    }

    [Fact]
    public void Likert_RejectsTwoAnswersToOneRow()
    {
        Answer(Likert(), "library=3", "library=4", "labs=1").Errors["services"]
            .Should().Equal("Each statement can only have one answer.");
    }

    [Fact]
    public void Likert_WhenRequired_EveryRowMustBeAnswered()
    {
        Answer(Likert(), "library=3").Errors["services"].Should().Equal("Please answer every statement.");
        Answer(Likert()).Errors["services"].Should().Equal("This question is required.");
    }

    [Fact]
    public void Likert_WhenOptional_SomeRowsCanBeLeftBlank()
    {
        var result = Answer(Likert(required: false), "labs=4");

        result.IsValid.Should().BeTrue();
        result.Answers["services"].Should().Equal("labs=4");
    }

    [Theory]
    [InlineData("0", true)]
    [InlineData("10", true)]
    [InlineData("07", true)]
    [InlineData("11", false)]
    [InlineData("-1", false)]
    [InlineData("8.5", false)]
    [InlineData("nine", false)]
    public void Nps_IsAWholeNumberFromZeroToTen(string answer, bool valid)
    {
        var result = Answer(Q(QuestionTypes.Nps), answer);

        result.IsValid.Should().Be(valid);
        if (valid)
        {
            result.Answers["q"].Should().Equal(int.Parse(answer).ToString());
        }
        else
        {
            result.Errors["q"].Should().Equal("Answer must be a whole number from 0 to 10.");
        }
    }

    [Theory]
    [InlineData("0", true)]
    [InlineData("100", true)]
    [InlineData("42", true)]
    [InlineData("101", false)]
    [InlineData("-1", false)]
    [InlineData("4.5", false)]
    public void Slider_DefaultsToZeroToOneHundred(string answer, bool valid)
    {
        var result = Answer(Q(QuestionTypes.Slider), answer);

        result.IsValid.Should().Be(valid);
        if (!valid)
        {
            result.Errors["q"].Should().Equal("Answer must be a whole number from 0 to 100.");
        }
    }

    [Fact]
    public void Slider_UsesTheQuestionsMinimumAndMaximum()
    {
        var question = Q(QuestionTypes.Slider, """[{"validationType":"MinValue","minValue":-5},{"validationType":"MaxValue","maxValue":5}]""");

        QuestionTypes.SliderRange(question).Should().Be((-5m, 5m));
        Answer(question, "-5").Answers["q"].Should().Equal("-5");
        Answer(question, "5.0").Answers["q"].Should().Equal("5");
        Answer(question, "6").Errors["q"].Should().Equal("Answer must be a whole number from -5 to 5.");
    }

    [Fact]
    public void SliderRange_ReadsARangeRule()
    {
        QuestionTypes.SliderRange(Q(QuestionTypes.Slider, """[{"validationType":"Range","minValue":1,"maxValue":7}]""")).Should().Be((1m, 7m));
    }

    [Fact]
    public void LikertQuestion_NeedsRows()
    {
        var question = Likert();
        question.Rows = [];

        _questions.Validate(question).Errors.Should().ContainSingle(e => e.Message == "A likert grid needs at least one row");
    }

    [Fact]
    public void LikertQuestion_WithoutOptions_IsValid()
    {
        _questions.Validate(Likert()).Valid.Should().BeTrue("a grid without options uses the default scale");
    }

    [Theory]
    [InlineData("", "Library", "Each row needs a label and a value")]
    [InlineData("labs", "Library", "Row values must be unique")]
    [InlineData("a=b", "Library", "Row values can't contain '='")]
    public void LikertQuestion_ChecksItsRows(string value, string label, string message)
    {
        var question = Likert();
        question.Rows[0] = new Option { Value = value, Label = label };

        _questions.Validate(question).Errors.Should().ContainSingle(e => e.Message == message);
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("""[{"validationType":"MinValue","minValue":1},{"validationType":"MaxValue","maxValue":7}]""", true)]
    [InlineData("""[{"validationType":"MaxValue","maxValue":0}]""", false)]
    [InlineData("""[{"validationType":"MinValue","minValue":10},{"validationType":"MaxValue","maxValue":5}]""", false)]
    [InlineData("""[{"validationType":"MaxValue","maxValue":10.5}]""", false)]
    public void SliderQuestion_NeedsWholeNumbers_WithTheMinimumBelowTheMaximum(string? rules, bool valid)
    {
        _questions.Validate(Q(QuestionTypes.Slider, rules)).Valid.Should().Be(valid);
    }

    [Fact]
    public void LikertAnswers_SplitAtTheFirstSeparator()
    {
        QuestionTypes.TryParseLikertAnswer("labs=a=b", out var row, out var option).Should().BeTrue();
        (row, option).Should().Be(("labs", "a=b"));
        QuestionTypes.TryParseLikertAnswer("labs", out _, out _).Should().BeFalse();
        QuestionTypes.LikertAnswer("labs", "4").Should().Be("labs=4");
    }
}
