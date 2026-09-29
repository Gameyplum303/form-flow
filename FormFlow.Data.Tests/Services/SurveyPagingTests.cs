using FluentAssertions;
using FormFlow.Data.Models;
using FormFlow.Data.Services;

namespace FormFlow.Data.Tests.Services;

public class SurveyPagingTests
{
    private static QuestionDefinition Question(string key, VisibleIf? visibleIf = null) =>
        new() { Id = Guid.NewGuid(), Key = key, Label = key, Type = QuestionTypes.YesNo, VisibleIf = visibleIf };

    private readonly QuestionDefinition _consent = Question("consent");
    private readonly QuestionDefinition _name;
    private readonly QuestionDefinition _email;
    private readonly QuestionDefinition _comments = Question("comments");

    public SurveyPagingTests()
    {
        _name = Question("name", new VisibleIf { Key = "consent", ShouldEqual = true });
        _email = Question("email", new VisibleIf { Key = "consent", ShouldEqual = true });
    }

    private List<QuestionDefinition> All => [_consent, _name, _email, _comments];

    [Fact]
    public void Normalize_KeepsQuestionsInTheSurvey_OtherThanTheFirst_InSurveyOrder()
    {
        var ids = All.Select(q => q.Id).ToList();

        var breaks = SurveyPaging.Normalize(ids, [_comments.Id, _consent.Id, Guid.NewGuid(), _name.Id, _comments.Id]);

        breaks.Should().Equal(_name.Id, _comments.Id);
    }

    [Fact]
    public void Normalize_WithoutBreaks_IsEmpty()
    {
        SurveyPaging.Normalize([_consent.Id], null).Should().BeEmpty();
    }

    [Fact]
    public void Split_WithoutBreaks_IsOnePage()
    {
        var pages = SurveyPaging.Split(All, []);

        pages.Should().ContainSingle().Which.Should().Equal(All);
    }

    [Fact]
    public void Split_StartsAPageAtEachBreak_AndIgnoresABreakOnTheFirstQuestion()
    {
        var pages = SurveyPaging.Split(All, [_consent.Id, _name.Id, _comments.Id]);

        pages.Select(p => p.Select(q => q.Key)).Should().BeEquivalentTo(
            new[] { new[] { "consent" }, new[] { "name", "email" }, new[] { "comments" } },
            o => o.WithStrictOrdering());
    }

    [Fact]
    public void Split_WithoutQuestions_IsOneEmptyPage()
    {
        SurveyPaging.Split([], [Guid.NewGuid()]).Should().ContainSingle().Which.Should().BeEmpty();
    }

    [Fact]
    public void ShownPages_SkipsAPageWhoseQuestionsAreAllHidden()
    {
        var pages = SurveyPaging.Split(All, [_name.Id, _comments.Id]);

        var declined = VisibilityEvaluator.VisibleKeys(All, new Dictionary<string, List<string>> { ["consent"] = ["false"] });
        var consented = VisibilityEvaluator.VisibleKeys(All, new Dictionary<string, List<string>> { ["consent"] = ["true"] });

        SurveyPaging.ShownPages(pages, declined).Should().Equal(0, 2);
        SurveyPaging.ShownPages(pages, consented).Should().Equal(0, 1, 2);
    }

    [Fact]
    public void ShownPages_IsNeverEmpty()
    {
        var pages = SurveyPaging.Split([_name], []);

        SurveyPaging.ShownPages(pages, new HashSet<string>()).Should().Equal(0);
    }

    [Fact]
    public void Errors_AreFoundByPage()
    {
        var pages = SurveyPaging.Split(All, [_name.Id, _comments.Id]);
        var errors = new Dictionary<string, string[]>
        {
            ["email"] = ["Answer must be an email address, like name@example.com."],
            ["comments"] = ["This question is required."],
        };

        SurveyPaging.FirstPageWithError(pages, errors).Should().Be(1);
        SurveyPaging.ErrorsOnPage(pages[2], errors).Keys.Should().Equal("comments");
        SurveyPaging.ErrorsOnPage(pages[0], errors).Should().BeEmpty();
        SurveyPaging.FirstPageWithError(pages, new Dictionary<string, string[]> { ["unknown"] = ["?"] }).Should().BeNull();
    }
}
