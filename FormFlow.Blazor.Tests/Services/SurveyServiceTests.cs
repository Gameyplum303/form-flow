using System.Net;
using System.Text;
using FluentAssertions;
using FormFlow.Blazor.Services;
using FormFlow.Data.Models;
using RichardSzalay.MockHttp;

namespace FormFlow.Blazor.Tests.Services;

public class SurveyServiceTests
{
    private readonly MockHttpMessageHandler _http = new();
    private readonly SurveyService _service;
    private readonly Guid _id = Guid.NewGuid();

    public SurveyServiceTests()
    {
        _service = new SurveyService(new HttpClient(_http) { BaseAddress = new Uri("http://api.test/") });
    }

    private static StringContent Json(string json) => new(json, Encoding.UTF8, "application/json");

    [Fact]
    public async Task GetSurveyAsync_returns_null_when_missing()
    {
        _http.When($"http://api.test/api/surveys/{_id}").Respond(HttpStatusCode.NotFound);

        (await _service.GetSurveyAsync(_id)).Should().BeNull();
    }

    [Fact]
    public async Task GetSurveyQuestionsAsync_returns_the_questions_in_order()
    {
        _http.When($"http://api.test/api/surveys/{_id}/questions")
            .Respond(Json("""[{"id":"11111111-1111-1111-1111-111111111111","key":"b","label":"B","type":"text"},{"id":"22222222-2222-2222-2222-222222222222","key":"a","label":"A","type":"text"}]"""));

        var questions = await _service.GetSurveyQuestionsAsync(_id);

        questions!.Select(q => q.Key).Should().Equal("b", "a");
    }

    [Fact]
    public async Task Reads_return_null_when_the_server_cannot_be_reached()
    {
        _http.When("*").Throw(new HttpRequestException("refused"));

        (await _service.GetSurveysAsync()).Should().BeNull();
        (await _service.GetManagedSurveysAsync()).Should().BeNull();
        (await _service.GetSurveyAsync(_id)).Should().BeNull();
        (await _service.GetSurveyQuestionsAsync(_id)).Should().BeNull();
        (await _service.GetResultsAsync(_id)).Should().BeNull();
        (await _service.ExportResponsesAsync(_id)).Should().BeNull();
        (await _service.HasAnsweredAsync(_id, "browser-1")).Should().BeFalse();
    }

    [Fact]
    public async Task Changes_say_when_the_server_cannot_be_reached()
    {
        _http.When("*").Throw(new HttpRequestException("refused"));

        (await _service.CreateSurveyAsync(new NewSurvey())).Error.Should().Be("Could not reach the server. Please try again.");
        (await _service.DeleteSurveyAsync(_id)).Error.Should().Be("Could not reach the server. Please try again.");
        (await _service.UpdateSharingAsync(_id, new SurveySharing())).Error.Should().Be("Could not reach the server. Please try again.");
    }

    [Fact]
    public async Task GetSurveyQuestionsAsync_returns_null_when_refused()
    {
        _http.When($"http://api.test/api/surveys/{_id}/questions").Respond(HttpStatusCode.NotFound);

        (await _service.GetSurveyQuestionsAsync(_id)).Should().BeNull();
    }

    [Fact]
    public async Task SubmitResponseAsync_sends_the_answers_and_reports_success()
    {
        _http.Expect(HttpMethod.Post, $"http://api.test/api/surveys/{_id}/responses")
            .WithContent("""{"answers":{"age":["30"]},"respondentId":"browser-1"}""")
            .Respond(HttpStatusCode.Created);

        var result = await _service.SubmitResponseAsync(_id, new() { ["age"] = ["30"] }, "browser-1");

        result.Success.Should().BeTrue();
        _http.VerifyNoOutstandingExpectation();
    }

    [Fact]
    public async Task SubmitResponseAsync_returns_the_errors_per_question_from_a_validation_problem()
    {
        _http.When(HttpMethod.Post, $"http://api.test/api/surveys/{_id}/responses")
            .Respond(HttpStatusCode.BadRequest, "application/problem+json",
                """{"title":"One or more validation errors occurred.","status":400,"errors":{"age":["Value must be ≤ 120."]}}""");

        var result = await _service.SubmitResponseAsync(_id, new() { ["age"] = ["130"] });

        result.Success.Should().BeFalse();
        result.Errors.Should().ContainKey("age").WhoseValue.Should().Equal("Value must be ≤ 120.");
        result.Message.Should().Be("Please fix the highlighted answers.");
    }

    [Fact]
    public async Task SubmitResponseAsync_reports_when_the_server_cannot_be_reached()
    {
        _http.When(HttpMethod.Post, $"http://api.test/api/surveys/{_id}/responses").Throw(new HttpRequestException("refused"));

        var result = await _service.SubmitResponseAsync(_id, new());

        result.Success.Should().BeFalse();
        result.Message.Should().Be("Could not reach the server. Please try again.");
    }

    [Fact]
    public async Task CreateSurveyAsync_returns_the_api_error_message()
    {
        _http.When(HttpMethod.Post, "http://api.test/api/surveys").Respond(HttpStatusCode.BadRequest, Json("""{"error":"Title is required."}"""));

        var (success, error) = await _service.CreateSurveyAsync(new NewSurvey());

        success.Should().BeFalse();
        error.Should().Be("Title is required.");
    }

    [Fact]
    public async Task DeleteSurveyAsync_describes_a_failure_without_a_body()
    {
        _http.When(HttpMethod.Delete, $"http://api.test/api/surveys/{_id}").Respond(HttpStatusCode.NotFound);

        var (success, error) = await _service.DeleteSurveyAsync(_id);

        success.Should().BeFalse();
        error.Should().Be("Request failed (404)");
    }

    [Fact]
    public async Task GetResultsAsync_reads_the_results()
    {
        _http.When($"http://api.test/api/surveys/{_id}/results")
            .Respond(Json($$"""{"surveyId":"{{_id}}","title":"Campus","totalResponses":3,"questions":[]}"""));

        var results = await _service.GetResultsAsync(_id);

        results!.TotalResponses.Should().Be(3);
    }

    [Fact]
    public async Task ExportResponsesAsync_returns_the_file_name_and_bytes()
    {
        var csv = new ByteArrayContent(Encoding.UTF8.GetBytes("response_id\r\n"));
        csv.Headers.ContentType = new("text/csv");
        csv.Headers.ContentDisposition = new("attachment") { FileName = "campus-responses.csv", FileNameStar = "campus-responses.csv" };
        _http.When($"http://api.test/api/surveys/{_id}/responses/export").Respond(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = csv });

        var export = await _service.ExportResponsesAsync(_id);

        export!.FileName.Should().Be("campus-responses.csv");
        Encoding.UTF8.GetString(export.Content).Should().Be("response_id\r\n");
    }

    [Fact]
    public async Task ExportResponsesAsync_returns_null_when_refused()
    {
        _http.When($"http://api.test/api/surveys/{_id}/responses/export").Respond(HttpStatusCode.Unauthorized);

        (await _service.ExportResponsesAsync(_id)).Should().BeNull();
    }

    [Fact]
    public async Task Changes_to_someone_elses_survey_say_why_they_were_refused()
    {
        _http.When(HttpMethod.Delete, $"http://api.test/api/surveys/{_id}").Respond(HttpStatusCode.Forbidden);

        var (success, error) = await _service.DeleteSurveyAsync(_id);

        success.Should().BeFalse();
        error.Should().Be("You can only change surveys and questions you created.");
    }

    [Fact]
    public async Task Requests_carry_the_signed_in_admins_token()
    {
        var session = new Auth.FakeSessionStorage().CreateSession();
        await session.SignInAsync(Auth.FakeSessionStorage.Login());
        var service = new SurveyService(new HttpClient(_http) { BaseAddress = new Uri("http://api.test/") }, session);
        _http.When(HttpMethod.Delete, $"http://api.test/api/surveys/{_id}")
            .WithHeaders("Authorization", "Bearer test-token")
            .Respond(HttpStatusCode.NoContent);

        var (success, _) = await service.DeleteSurveyAsync(_id);

        success.Should().BeTrue();
    }

    [Theory]
    [InlineData(HttpStatusCode.Conflict, """{"error":"You've already answered this survey. Thank you!"}""", "You've already answered this survey. Thank you!")]
    [InlineData(HttpStatusCode.NotFound, "{}", "This survey is no longer available.")]
    public async Task SubmitResponseAsync_reports_a_refusal_that_trying_again_wont_fix(HttpStatusCode status, string body, string message)
    {
        _http.When(HttpMethod.Post, $"http://api.test/api/surveys/{_id}/responses").Respond(status, Json(body));

        var result = await _service.SubmitResponseAsync(_id, new() { ["age"] = ["30"] }, "browser-1");

        result.Success.Should().BeFalse();
        result.CanRetry.Should().BeFalse();
        result.Message.Should().Be(message);
    }

    [Fact]
    public async Task GetSurveyByShareCodeAsync_opens_the_survey_or_returns_null()
    {
        _http.When("http://api.test/api/share/k7m2p9qa")
            .Respond(Json($$"""{"id":"{{_id}}","title":"Campus","description":"","questionIds":[],"createdAt":"2026-09-29T00:00:00Z","status":"published","shareCode":"k7m2p9qa"}"""));
        _http.When("http://api.test/api/share/nope2345").Respond(HttpStatusCode.NotFound);

        (await _service.GetSurveyByShareCodeAsync("k7m2p9qa"))!.Id.Should().Be(_id);
        (await _service.GetSurveyByShareCodeAsync("nope2345")).Should().BeNull();
    }

    [Fact]
    public async Task UpdateSharingAsync_returns_the_saved_survey_or_the_servers_error()
    {
        _http.Expect(HttpMethod.Put, $"http://api.test/api/surveys/{_id}/sharing")
            .WithPartialContent("\"status\":\"published\"")
            .Respond(Json($$"""{"id":"{{_id}}","title":"Campus","description":"","questionIds":[],"createdAt":"2026-09-29T00:00:00Z","status":"published","listed":false}"""));

        var (saved, error) = await _service.UpdateSharingAsync(_id, new SurveySharing { Status = SurveyStatuses.Published });

        error.Should().BeNull();
        saved!.IsPublished().Should().BeTrue();
        saved.Listed.Should().BeFalse();
        _http.VerifyNoOutstandingExpectation();

        _http.Clear();
        _http.When(HttpMethod.Put, $"http://api.test/api/surveys/{_id}/sharing")
            .Respond(HttpStatusCode.BadRequest, Json("""{"error":"Status must be draft or published."}"""));
        (await _service.UpdateSharingAsync(_id, new SurveySharing { Status = "secret" })).Error
            .Should().Be("Status must be draft or published.");
    }

    [Fact]
    public async Task HasAnsweredAsync_asks_about_this_browsers_respondent_id()
    {
        _http.When($"http://api.test/api/surveys/{_id}/answered")
            .WithQueryString("respondentId", "browser-1")
            .Respond(Json("""{"answered":true}"""));

        (await _service.HasAnsweredAsync(_id, "browser-1")).Should().BeTrue();
    }
}
