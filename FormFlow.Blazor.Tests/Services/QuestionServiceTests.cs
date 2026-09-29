using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FormFlow.Blazor.Services;
using FormFlow.Data.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;

namespace FormFlow.Blazor.Tests.Services;

public class QuestionServiceTests
{
    private readonly Mock<HttpMessageHandler> _handlerMock;
    private readonly HttpClient _httpClient;
    private readonly QuestionService _service;
    private const string BaseUrl = "https://test.local/";

    public QuestionServiceTests()
    {
        _handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        _httpClient = new HttpClient(_handlerMock.Object)
        {
            BaseAddress = new Uri(BaseUrl)
        };
        _service = new QuestionService(_httpClient, NullLogger<QuestionService>.Instance);
    }

    [Fact]
    public async Task GetAllQuestionsAsync_Success_ReturnsData()
    {
        // Arrange: Simulate a successful 200 OK with two questions
        var mockData = BuildMockQuestions();

        SetupMockResponse(HttpStatusCode.OK, mockData);

        // Act
        var result = await _service.GetAllQuestionsAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count());
        Assert.Equal("day", result.First().Key);
    }

    [Fact]
    public async Task GetAllQuestionsAsync_RequestsTheQuestionsUnderTheBaseAddress()
    {
        HttpRequestMessage? sent = null;
        SetupMockResponse(HttpStatusCode.OK, BuildMockQuestions(), request => sent = request);

        await _service.GetAllQuestionsAsync();

        Assert.Equal($"{BaseUrl}api/questions", sent!.RequestUri!.ToString());
    }

    [Fact]
    public async Task GetAllQuestionsAsync_ApiError_ReturnsNull()
    {
        // Null, not an empty list, so pages can tell "no questions" from "could not load them".
        SetupMockResponse(HttpStatusCode.InternalServerError, "Error message");

        var result = await _service.GetAllQuestionsAsync();

        Assert.Null(result);
    }

    [Fact]
    public async Task GetAllQuestionsAsync_NotFound_ReturnsNull()
    {
        SetupMockResponse(HttpStatusCode.NotFound, null);

        var result = await _service.GetAllQuestionsAsync();

        Assert.Null(result);
    }

    [Fact]
    public async Task GetAllQuestionsAsync_Unreachable_ReturnsNull()
    {
        SetupMockFailure(new HttpRequestException("refused"));

        var result = await _service.GetAllQuestionsAsync();

        Assert.Null(result);
    }

    [Fact]
    public async Task GetQuestionAsync_Unreachable_ReturnsNull()
    {
        SetupMockFailure(new HttpRequestException("refused"));

        var result = await _service.GetQuestionAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task CreateQuestionAsync_Unreachable_SaysSo()
    {
        SetupMockFailure(new HttpRequestException("refused"));

        var (success, error) = await _service.CreateQuestionAsync(BuildNewQuestion());

        Assert.False(success);
        Assert.Equal("Could not reach the server. Please try again.", error);
    }

    [Fact]
    public async Task DeleteQuestionAsync_Forbidden_SaysOnlyTheCreatorCanChangeIt()
    {
        SetupMockResponse(HttpStatusCode.Forbidden, null);

        var (success, error) = await _service.DeleteQuestionAsync(Guid.NewGuid());

        Assert.False(success);
        Assert.Equal("You can only change surveys and questions you created.", error);
    }

    [Fact]
    public async Task CreateQuestionAsync_Success_ReturnsSuccessTrue()
    {
        SetupMockResponse(HttpStatusCode.Created, null);

        var (success, error) = await _service.CreateQuestionAsync(BuildNewQuestion());

        Assert.True(success);
        Assert.Null(error);
    }
    [Fact]
    public async Task CreateQuestionAsync_Conflict_ReturnsSuccessFalseWithMessage()
    {
        SetupMockResponse(HttpStatusCode.Conflict, "A question with key 'age' already exists");

        var (success, error) = await _service.CreateQuestionAsync(BuildNewQuestion());

        Assert.False(success);
        Assert.Equal("A question with key 'age' already exists", error);
    }
    [Fact]
    public async Task CreateQuestionAsync_ServerError_ReturnsSuccessFalseWithMessage()
    {
        SetupMockResponse(HttpStatusCode.InternalServerError, "Internal Server Error");

        var (success, error) = await _service.CreateQuestionAsync(BuildNewQuestion());

        Assert.False(success);
        Assert.Equal("Internal Server Error", error);
    }

    // Helpers
    private void SetupMockResponse(HttpStatusCode code, object? content, Action<HttpRequestMessage>? onSend = null)
    {
        var response = new HttpResponseMessage
        {
            StatusCode = code,
            Content = new StringContent(JsonSerializer.Serialize(content))
        };

        _handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .Callback<HttpRequestMessage, CancellationToken>((request, _) => onSend?.Invoke(request))
            .ReturnsAsync(response);
    }

    private void SetupMockFailure(Exception exception)
    {
        _handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ThrowsAsync(exception);
    }
    private static NewQuestion BuildNewQuestion() => new()
    {
        Label = "Age",
        Key = "age",
        Type = "number",
        Required = true
    };
    private static List<QuestionDefinition> BuildMockQuestions() => new()
    {
        new()
        {
            Key = "day",
            Label = "Day",
            Type = "text",
            Required = true,
            Placeholder = "Enter the day"
        },
        new()
        {
            Key = "month",
            Label = "Month",
            Type = "dropdown",
            Required = true,
            Placeholder = "Enter the month",
            Options = new List<Option>
            {
                new() { Value = "January",  Label = "January" },
                new() { Value = "February", Label = "February" }

            }
        }
    };

}