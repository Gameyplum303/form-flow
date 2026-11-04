using FluentAssertions;
using FormFlow.Data.Models;
using FormFlow.Data.Services;

namespace FormFlow.Tests.Services;

/// <summary>QuestionValidator's checks of a quiz answer key.</summary>
public class AnswerKeyValidationTests
{
    private readonly QuestionValidator _validator = new();

    private static readonly List<Option> Options =
    [
        new() { Value = "a", Label = "A" },
        new() { Value = "b", Label = "B" },
        new() { Value = "c", Label = "C" },
    ];

    private static QuestionDefinition Q(string type, params string[] correct) => new()
    {
        Id = Guid.NewGuid(),
        Key = "k",
        Label = "L",
        Type = type,
        Options = QuestionTypes.IsChoice(type) ? Options.ToList() : [],
        AnswerKey = new AnswerKey { CorrectAnswers = correct.ToList() },
    };

    private IEnumerable<string?> Errors(QuestionDefinition question) => _validator.Validate(question).Errors.Select(e => e.Message);

    [Theory]
    [InlineData(QuestionTypes.Radio, "b")]
    [InlineData(QuestionTypes.Dropdown, "a")]
    [InlineData(QuestionTypes.YesNo, "true")]
    [InlineData(QuestionTypes.YesNo, "false")]
    [InlineData(QuestionTypes.Checkbox, "a", "c")]
    [InlineData(QuestionTypes.Multiselect, "b")]
    [InlineData(QuestionTypes.Number, "-2.5")]
    [InlineData(QuestionTypes.Text, "Paris", "paris, france")]
    public void SuitableAnswerKeys_AreValid(string type, params string[] correct)
    {
        _validator.Validate(Q(type, correct)).Valid.Should().BeTrue();
    }

    [Fact]
    public void NoAnswerKey_IsValid()
    {
        var question = Q(QuestionTypes.Radio);
        question.AnswerKey = null;

        _validator.Validate(question).Valid.Should().BeTrue();
    }

    [Theory]
    [InlineData(QuestionTypes.Rating)]
    [InlineData(QuestionTypes.LongText)]
    [InlineData(QuestionTypes.Likert)]
    [InlineData(QuestionTypes.Date)]
    public void TypesThatCantBeGraded_CantHaveAnAnswerKey(string type)
    {
        var question = Q(type, "5");
        question.Rows = [new Option { Value = "r", Label = "Row" }];

        Errors(question).Should().Contain(m => m!.StartsWith("Only these question types can have an answer key"));
    }

    [Fact]
    public void CheckboxWithoutOptions_CantHaveAnAnswerKey()
    {
        var question = Q(QuestionTypes.Checkbox, "true");
        question.Options = [];

        Errors(question).Should().Contain("A checkbox needs options to have an answer key");
    }

    [Theory]
    [InlineData]
    [InlineData("")]
    [InlineData("a", "  ")]
    public void CorrectAnswers_AreRequired_AndCantBeBlank(params string[] correct)
    {
        Errors(Q(QuestionTypes.Multiselect, correct)).Should()
            .Contain("An answer key needs at least one correct answer, and none can be empty");
    }

    [Fact]
    public void MissingCorrectAnswersList_IsRejected()
    {
        var question = Q(QuestionTypes.Text);
        question.AnswerKey!.CorrectAnswers = null!;

        Errors(question).Should().Contain("An answer key needs at least one correct answer, and none can be empty");
    }

    [Theory]
    [InlineData(QuestionTypes.Radio, "radio")]
    [InlineData(QuestionTypes.Dropdown, "dropdown")]
    [InlineData(QuestionTypes.YesNo, "yes/no")]
    [InlineData(QuestionTypes.Number, "number")]
    public void SingleAnswerTypes_HaveOneCorrectAnswer(string type, string name)
    {
        var correct = type switch { QuestionTypes.YesNo => new[] { "true", "false" }, QuestionTypes.Number => ["1", "2"], _ => ["a", "b"] };

        Errors(Q(type, correct)).Should().Contain($"A {name} question has one correct answer");
    }

    [Theory]
    [InlineData(QuestionTypes.Radio)]
    [InlineData(QuestionTypes.Checkbox)]
    [InlineData(QuestionTypes.Multiselect)]
    public void CorrectOptions_MustBeOptions(string type)
    {
        Errors(Q(type, "z")).Should().Contain("The correct answer 'z' is not one of the options");
    }

    [Fact]
    public void CorrectOptions_AreListedOnce()
    {
        Errors(Q(QuestionTypes.Checkbox, "a", "a")).Should().Contain("Each correct option can only be listed once");
    }

    [Fact]
    public void YesNo_NeedsTrueOrFalse()
    {
        Errors(Q(QuestionTypes.YesNo, "maybe")).Should().Contain("The correct answer to a yes/no question must be true or false");
    }

    [Fact]
    public void Number_NeedsANumber()
    {
        Errors(Q(QuestionTypes.Number, "ten")).Should().Contain("The correct answer to a number question must be a number");
    }

    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, true)]
    [InlineData(2.5, true)]
    [InlineData(1000, true)]
    [InlineData(1000.5, false)]
    public void Points_RunFromZeroToTheMaximum(decimal points, bool valid)
    {
        var question = Q(QuestionTypes.Text, "x");
        question.AnswerKey!.Points = points;

        _validator.Validate(question).Valid.Should().Be(valid);
        if (!valid)
        {
            Errors(question).Should().Contain("Points must be from 0 to 1000");
        }
    }

    [Fact]
    public void Explanation_HasAMaximumLength()
    {
        var question = Q(QuestionTypes.Text, "x");
        question.AnswerKey!.Explanation = new string('e', AnswerKey.MaxExplanationLength);
        _validator.Validate(question).Valid.Should().BeTrue();

        question.AnswerKey.Explanation += "e";
        Errors(question).Should().Contain("The explanation can be at most 2000 characters");
    }

    [Fact]
    public void AnswerKeyErrors_NameTheirField()
    {
        var question = Q(QuestionTypes.Radio, "z");
        question.AnswerKey!.Points = -1;

        _validator.Validate(question).Errors.Select(e => e.Field).Should()
            .BeEquivalentTo("answerKey.points", "answerKey.correctAnswers");
    }
}
