using FluentAssertions;
using FormFlow.Data.Models;
using Xunit;

namespace FormFlow.Data.Tests.Models;

public class QuestionTypesTests
{
    [Fact]
    public void EveryType_HasADisplayName()
    {
        foreach (var type in QuestionTypes.All)
        {
            QuestionTypes.DisplayName(type).Should().NotBe(type, $"{type} should have a readable name");
        }
    }

    [Theory]
    [InlineData("yes_no", "Yes/No")]
    [InlineData("LONG_TEXT", "Long text")]
    [InlineData("likert", "Likert grid")]
    [InlineData("NPS", "NPS (0–10)")]
    [InlineData("matrix", "matrix")]
    [InlineData(null, "")]
    public void DisplayName_NamesKnownTypes_AndLeavesOthersAlone(string? type, string expected)
    {
        QuestionTypes.DisplayName(type).Should().Be(expected);
    }
}
