using FluentAssertions;
using FormFlow.Data.Models;
using FormFlow.Data.Services;

namespace FormFlow.Data.Tests.Services;

public class VisibilityEvaluatorTests
{
    private static QuestionDefinition Q(string key, VisibleIf? visibleIf = null) =>
        new() { Id = Guid.NewGuid(), Key = key, Label = key, Type = "yes_no", VisibleIf = visibleIf };

    private static Dictionary<string, List<string>> Answers(params (string Key, string Value)[] answers) =>
        answers.ToDictionary(a => a.Key, a => new List<string> { a.Value });

    [Fact]
    public void QuestionsWithoutRules_AreAlwaysVisible()
    {
        var visible = VisibilityEvaluator.VisibleKeys([Q("a"), Q("b")], Answers());

        visible.Should().BeEquivalentTo(["a", "b"]);
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("Yes", true)]
    [InlineData("false", false)]
    [InlineData("no", false)]
    public void RuleMatchesControllingAnswer(string answer, bool shown)
    {
        var questions = new[] { Q("student"), Q("campus", new VisibleIf { Key = "student", ShouldEqual = true }) };

        var visible = VisibilityEvaluator.VisibleKeys(questions, Answers(("student", answer)));

        visible.Contains("campus").Should().Be(shown);
    }

    [Fact]
    public void UnansweredControllingQuestion_HidesDependent()
    {
        var questions = new[] { Q("student"), Q("campus", new VisibleIf { Key = "student", ShouldEqual = false }) };

        VisibilityEvaluator.VisibleKeys(questions, Answers()).Should().NotContain("campus");
    }

    [Fact]
    public void HiddenControllingQuestion_HidesItsDependentsToo()
    {
        var questions = new[]
        {
            Q("a"),
            Q("b", new VisibleIf { Key = "a", ShouldEqual = true }),
            Q("c", new VisibleIf { Key = "b", ShouldEqual = true })
        };

        // b has a stale "true" answer but is hidden because a is false.
        var visible = VisibilityEvaluator.VisibleKeys(questions, Answers(("a", "false"), ("b", "true")));

        visible.Should().BeEquivalentTo(["a"]);
    }

    [Fact]
    public void RuleOnMissingQuestionOrCycle_HidesQuestion()
    {
        var questions = new[]
        {
            Q("orphan", new VisibleIf { Key = "not_in_survey", ShouldEqual = true }),
            Q("x", new VisibleIf { Key = "y", ShouldEqual = true }),
            Q("y", new VisibleIf { Key = "x", ShouldEqual = true })
        };

        VisibilityEvaluator.VisibleKeys(questions, Answers(("x", "true"), ("y", "true"))).Should().BeEmpty();
    }
}
