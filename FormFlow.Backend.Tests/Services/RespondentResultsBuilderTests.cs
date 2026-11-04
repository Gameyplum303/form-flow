using FluentAssertions;
using FormFlow.Backend.Services;
using FormFlow.Data.Models;

namespace FormFlow.Backend.Tests.Services
{
    public class RespondentResultsBuilderTests
    {
        private static readonly SurveyDefinition Survey = new()
        {
            Id = Guid.NewGuid(),
            Title = "Lecture poll",
            Description = "",
            QuestionIds = [],
            CreatedAt = DateTime.UtcNow,
            OwnerId = Guid.NewGuid(),
            OwnerName = "prof@example.com",
        };

        private static QuestionDefinition Question(string key, string type, params string[] options) => new()
        {
            Id = Guid.NewGuid(),
            Key = key,
            Label = key,
            Type = type,
            Options = options.Select(o => new Option { Value = o, Label = o.ToUpperInvariant() }).ToList(),
        };

        private static SurveyResponse Response(Dictionary<string, List<string>> answers) => new()
        {
            Id = Guid.NewGuid(),
            SurveyId = Survey.Id,
            SubmittedAt = DateTime.UtcNow,
            SubmittedBy = "student@example.com",
            RespondentId = "browser-1",
            Answers = answers,
        };

        [Theory]
        [InlineData(QuestionTypes.YesNo, true)]
        [InlineData(QuestionTypes.Dropdown, true)]
        [InlineData(QuestionTypes.Radio, true)]
        [InlineData(QuestionTypes.Checkbox, true)]
        [InlineData(QuestionTypes.Multiselect, true)]
        [InlineData(QuestionTypes.Rating, true)]
        [InlineData(QuestionTypes.Nps, true)]
        [InlineData(QuestionTypes.Text, false)]
        [InlineData(QuestionTypes.LongText, false)]
        [InlineData(QuestionTypes.Email, false)]
        [InlineData(QuestionTypes.Number, false)]
        [InlineData(QuestionTypes.Date, false)]
        [InlineData(QuestionTypes.Slider, false)]
        [InlineData(QuestionTypes.Likert, false)]
        public void OnlyQuestionsWithFixedAnswers_AreIncluded(string type, bool included)
        {
            RespondentResultsBuilder.IsIncluded(Question("q", type, "a", "b")).Should().Be(included);
        }

        [Fact]
        public void PercentagesAreOfTheResponsesThatAnsweredTheQuestion()
        {
            var campus = Question("campus", QuestionTypes.Radio, "north", "south", "east");
            var skills = Question("skills", QuestionTypes.Multiselect, "csharp", "sql");
            var responses = new[]
            {
                Response(new() { ["campus"] = ["north"], ["skills"] = ["csharp", "sql"] }),
                Response(new() { ["campus"] = ["north"], ["skills"] = ["sql"] }),
                Response(new() { ["campus"] = ["south"] }),
                Response(new()),
            };

            var summary = RespondentResultsBuilder.Build(Survey, [campus, skills], responses);

            summary.TotalResponses.Should().Be(4);
            summary.Questions[0].AnsweredCount.Should().Be(3);
            summary.Questions[0].Options.Select(o => (o.Label, o.Count, o.Percent))
                .Should().Equal(("NORTH", 2, 67), ("SOUTH", 1, 33), ("EAST", 0, 0));
            summary.Questions[1].MultipleAnswers.Should().BeTrue();
            summary.Questions[1].Options.Select(o => o.Percent).Should().Equal(50, 100);
        }

        [Fact]
        public void AQuestionNobodyAnswered_HasZeroPercentEverywhere()
        {
            var summary = RespondentResultsBuilder.Build(Survey, [Question("campus", QuestionTypes.Radio, "north")], []);

            summary.Questions.Single().Options.Single().Percent.Should().Be(0);
        }

        [Fact]
        public void ASingleTickBox_IsOneAnswerPerResponse()
        {
            var agree = Question("agree", QuestionTypes.Checkbox);

            var summary = RespondentResultsBuilder.Build(Survey, [agree], [Response(new() { ["agree"] = ["true"] })]);

            summary.Questions.Single().MultipleAnswers.Should().BeFalse();
            summary.Questions.Single().Options.Select(o => (o.Label, o.Percent)).Should().Equal(("Checked", 100), ("Not checked", 0));
        }

        [Fact]
        public void TextAnswers_NeverReachTheSummary()
        {
            var campus = Question("campus", QuestionTypes.Radio, "north");
            var comments = Question("comments", QuestionTypes.LongText);

            var summary = RespondentResultsBuilder.Build(Survey, [comments, campus],
                [Response(new() { ["campus"] = ["north"], ["comments"] = ["Please call me on 555-0100"] })]);

            summary.Questions.Select(q => q.Key).Should().Equal("campus");
            System.Text.Json.JsonSerializer.Serialize(summary).Should()
                .NotContainAny("555-0100", "student@example.com", "browser-1", "prof@example.com");
        }
    }
}
