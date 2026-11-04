using FluentAssertions;
using FormFlow.Data.Models;
using FormFlow.Data.Services;

namespace FormFlow.Tests.Services;

public class QuizScorerTests
{
    private static readonly List<Option> Planets =
    [
        new() { Value = "mercury", Label = "Mercury" },
        new() { Value = "venus", Label = "Venus" },
        new() { Value = "earth", Label = "Earth" },
        new() { Value = "mars", Label = "Mars" },
    ];

    private static QuestionDefinition Question(string type, string[] correct, decimal points = 1, List<Option>? options = null,
        string key = "q", VisibleIf? visibleIf = null, string? explanation = null) => new()
        {
            Id = Guid.NewGuid(),
            Key = key,
            Label = $"Question {key}",
            Type = type,
            Options = options ?? [],
            VisibleIf = visibleIf,
            AnswerKey = new AnswerKey { CorrectAnswers = correct.ToList(), Points = points, Explanation = explanation },
        };

    private static QuestionGrade Grade(QuestionDefinition question, params string[] answer) => QuizScorer.Grade(question, answer);

    [Theory]
    [InlineData(QuestionTypes.Radio, "venus", true)]
    [InlineData(QuestionTypes.Radio, "mars", false)]
    [InlineData(QuestionTypes.Dropdown, "venus", true)]
    [InlineData(QuestionTypes.Dropdown, "Venus", false)]
    public void OneCorrectOption_MustMatchExactly(string type, string answer, bool correct)
    {
        var grade = Grade(Question(type, ["venus"], points: 2, options: Planets), answer);

        grade.Correct.Should().Be(correct);
        grade.Points.Should().Be(correct ? 2 : 0);
        grade.MaxPoints.Should().Be(2);
    }

    [Theory]
    [InlineData("true", "true", true)]
    [InlineData("true", "yes", true)]
    [InlineData("false", "false", true)]
    [InlineData("false", "true", false)]
    public void YesNo_ComparesTheAnswer(string key, string answer, bool correct)
    {
        Grade(Question(QuestionTypes.YesNo, [key]), answer).Correct.Should().Be(correct);
    }

    [Theory]
    [InlineData("42", "42", true)]
    [InlineData("42", "42.0", true)]
    [InlineData("0.5", ".5", true)]
    [InlineData("42", "41.99", false)]
    [InlineData("42", "forty-two", false)]
    public void Number_NeedsTheExactValue(string key, string answer, bool correct)
    {
        Grade(Question(QuestionTypes.Number, [key]), answer).Correct.Should().Be(correct);
    }

    [Theory]
    [InlineData("Paris", true)]
    [InlineData("  paris ", true)]
    [InlineData("PARIS", true)]
    [InlineData("City of Light", true)]
    [InlineData("Lyon", false)]
    [InlineData("Par is", false)]
    public void Text_AcceptsAnyAcceptedAnswer_IgnoringCaseAndOuterSpaces(string answer, bool correct)
    {
        Grade(Question(QuestionTypes.Text, ["Paris", " city of light "]), answer).Correct.Should().Be(correct);
    }

    [Theory]
    [InlineData(new[] { "mercury", "venus" }, 2, true)]
    [InlineData(new[] { "venus", "mercury" }, 2, true)]
    [InlineData(new[] { "mercury" }, 1, false)]
    [InlineData(new[] { "mercury", "venus", "earth" }, 1, false)]
    [InlineData(new[] { "mercury", "earth" }, 0, false)]
    [InlineData(new[] { "mercury", "venus", "earth", "mars" }, 0, false)]
    [InlineData(new[] { "earth", "mars" }, 0, false)]
    public void CheckboxAndMultiselect_GivePartialCredit_AndTakeItAwayForWrongPicks(string[] answer, decimal points, bool correct)
    {
        foreach (var type in new[] { QuestionTypes.Checkbox, QuestionTypes.Multiselect })
        {
            var grade = Grade(Question(type, ["mercury", "venus"], points: 2, options: Planets), answer);

            grade.Points.Should().Be(points, type);
            grade.Correct.Should().Be(correct, type);
        }
    }

    [Fact]
    public void PartialCredit_IsRoundedToTwoDecimalPlaces()
    {
        var grade = Grade(Question(QuestionTypes.Multiselect, ["mercury", "venus", "earth"], options: Planets), "mercury");

        grade.Points.Should().Be(0.33m);
    }

    [Fact]
    public void Unanswered_EarnsNothing()
    {
        var question = Question(QuestionTypes.Radio, ["venus"], options: Planets);

        QuizScorer.Grade(question, null).Should().Match<QuestionGrade>(g => g.Points == 0 && !g.Correct && g.GivenAnswers.Count == 0);
        QuizScorer.Grade(question, []).Correct.Should().BeFalse();
    }

    [Fact]
    public void Grade_DescribesAnswersAsRespondentsSawThem()
    {
        var grade = Grade(Question(QuestionTypes.Multiselect, ["mercury", "venus"], options: Planets, explanation: "  Closest to the sun.  "),
            "mercury", "mars");

        grade.GivenAnswers.Should().Equal("Mercury", "Mars");
        grade.CorrectAnswers.Should().Equal("Mercury", "Venus");
        grade.Explanation.Should().Be("Closest to the sun.");

        var yesNo = Grade(Question(QuestionTypes.YesNo, ["false"]), "true");
        yesNo.GivenAnswers.Should().Equal("Yes");
        yesNo.CorrectAnswers.Should().Equal("No");

        Grade(Question(QuestionTypes.Text, [" Paris "]), "paris").CorrectAnswers.Should().Equal("Paris");
        Grade(Question(QuestionTypes.Text, ["Paris"], explanation: " "), "paris").Explanation.Should().BeNull();
    }

    [Fact]
    public void Score_AddsUpTheGradedQuestions()
    {
        List<QuestionDefinition> questions =
        [
            Question(QuestionTypes.Radio, ["venus"], points: 2, options: Planets, key: "hottest"),
            Question(QuestionTypes.Number, ["8"], points: 3, key: "planets"),
            Question(QuestionTypes.Multiselect, ["mercury", "venus"], points: 2, options: Planets, key: "inner_no_moons"),
            Question(QuestionTypes.Text, ["Mars"], points: 1, key: "red"),
        ];
        var answers = new Dictionary<string, List<string>>
        {
            ["hottest"] = ["venus"],
            ["planets"] = ["9"],
            ["inner_no_moons"] = ["mercury"],
            ["red"] = ["mars"],
        };

        var result = QuizScorer.Score(questions, answers);

        result.Score.Should().Be(4);
        result.MaxScore.Should().Be(8);
        result.Percent.Should().Be(50);
        result.Questions.Select(q => q.Key).Should().Equal("hottest", "planets", "inner_no_moons", "red");
        result.Questions.Select(q => q.Correct).Should().Equal(true, false, false, true);
    }

    [Fact]
    public void Score_LeavesOutQuestionsWithoutAnAnswerKey_AndTypesThatCantBeGraded()
    {
        var ungraded = Question(QuestionTypes.Radio, [], options: Planets, key: "opinion");
        ungraded.AnswerKey = null;
        List<QuestionDefinition> questions =
        [
            Question(QuestionTypes.Radio, ["venus"], options: Planets, key: "hottest"),
            ungraded,
            Question(QuestionTypes.Radio, [], options: Planets, key: "empty_key"),
            Question(QuestionTypes.Rating, ["5"], key: "stars"),
            Question(QuestionTypes.Checkbox, ["true"], key: "tick_box"),
        ];

        var result = QuizScorer.Score(questions, new Dictionary<string, List<string>>
        {
            ["hottest"] = ["venus"],
            ["opinion"] = ["mars"],
            ["stars"] = ["5"],
            ["tick_box"] = ["true"],
        });

        result.Questions.Select(q => q.Key).Should().Equal("hottest");
        result.Score.Should().Be(1);
        result.MaxScore.Should().Be(1);
        result.Percent.Should().Be(100);
    }

    [Fact]
    public void Score_LeavesOutHiddenQuestions_ButCountsUnansweredVisibleOnes()
    {
        List<QuestionDefinition> questions =
        [
            Question(QuestionTypes.YesNo, ["true"], key: "took_astronomy"),
            Question(QuestionTypes.Number, ["8"], points: 4, key: "planets", visibleIf: new VisibleIf { Key = "took_astronomy", ShouldEqual = true }),
            Question(QuestionTypes.Text, ["Mars"], points: 2, key: "red"),
        ];

        var hidden = QuizScorer.Score(questions, new Dictionary<string, List<string>> { ["took_astronomy"] = ["false"] });
        hidden.MaxScore.Should().Be(3);
        hidden.Score.Should().Be(0);
        hidden.Questions.Select(q => q.Key).Should().Equal("took_astronomy", "red");

        var shown = QuizScorer.Score(questions, new Dictionary<string, List<string>> { ["took_astronomy"] = ["true"], ["planets"] = ["8"] });
        shown.MaxScore.Should().Be(7);
        shown.Score.Should().Be(5);
        shown.Percent.Should().Be(71);
    }

    [Fact]
    public void Score_WithNothingToGrade_IsZeroOfZero()
    {
        var result = QuizScorer.Score([Question(QuestionTypes.Rating, ["5"])], new Dictionary<string, List<string>>());

        result.Should().Match<QuizResult>(r => r.Score == 0 && r.MaxScore == 0 && r.Percent == 0 && r.Questions.Count == 0);
    }

    [Fact]
    public void ZeroPointQuestions_AreReviewedButAddNothing()
    {
        var result = QuizScorer.Score([Question(QuestionTypes.YesNo, ["true"], points: 0)],
            new Dictionary<string, List<string>> { ["q"] = ["true"] });

        result.Questions.Should().ContainSingle().Which.Correct.Should().BeTrue();
        result.MaxScore.Should().Be(0);
        result.Percent.Should().Be(0);
    }

    [Theory]
    [InlineData(2, 3, 67)]
    [InlineData(1, 8, 13)]
    [InlineData(0.5, 1, 50)]
    [InlineData(1, 200, 1)]
    [InlineData(1, 0, 0)]
    public void Percent_RoundsHalvesUp(decimal score, decimal max, int percent)
    {
        QuizScorer.Percent(score, max).Should().Be(percent);
    }

    [Fact]
    public void QuestionTypes_IsGradable_CoversChoiceNumberAndTextTypes()
    {
        QuestionTypes.All.Where(t => QuestionTypes.IsGradable(t, optionCount: 2)).Should()
            .BeEquivalentTo(QuestionTypes.Radio, QuestionTypes.Dropdown, QuestionTypes.YesNo, QuestionTypes.Checkbox,
                QuestionTypes.Multiselect, QuestionTypes.Number, QuestionTypes.Text);
        QuestionTypes.IsGradable(QuestionTypes.Checkbox, optionCount: 0).Should().BeFalse();
        QuestionTypes.IsGradable("RADIO", optionCount: 2).Should().BeTrue();
        QuestionTypes.IsGradable(null, optionCount: 2).Should().BeFalse();
    }
}
