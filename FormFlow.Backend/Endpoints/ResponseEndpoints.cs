using System.Security.Claims;
using System.Text;
using System.Text.Json;
using FormFlow.Backend.Auth;
using FormFlow.Backend.Repositories;
using FormFlow.Backend.Services;
using FormFlow.Data.Models;
using FormFlow.Data.Services;

namespace FormFlow.Backend.Endpoints
{
    /// <summary>
    /// Body of a survey submission. Each answer may be a string, number, boolean,
    /// or an array of those for checkbox and multiselect questions.
    /// </summary>
    public class SubmitResponseRequest
    {
        public Dictionary<string, JsonElement> Answers { get; set; } = new();

        /// <summary>The respondent's temporary id for this browser (see <see cref="SurveyResponse.RespondentId"/>).</summary>
        public string? RespondentId { get; set; }
    }

    public static class ResponseEndpoints
    {
        public const string SubmitRateLimit = "submissions";
        public const string ClosedMessage = "This survey is closed and no longer takes answers.";
        public const string AlreadyAnsweredMessage = "You've already answered this survey. Thank you!";
        private const int MaxRespondentIdLength = 64;

        public static void MapResponseEndpoints(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("/api/surveys/{surveyId:guid}").WithTags("Responses");

            // Anyone can answer a survey. When the person is signed in, the response records who sent it.
            group.MapPost("/responses", (Guid surveyId, SubmitResponseRequest request, ClaimsPrincipal principal,
                ISurveyRepository surveys, IQuestionRepository questionRepository, IResponseRepository responses,
                ResponseValidator validator, TimeProvider clock) =>
            {
                var survey = surveys.FindById(surveyId);
                var user = CurrentUser.From(principal);
                if (survey is null || !user.CanOpen(survey))
                {
                    return Results.NotFound();
                }
                if (survey.IsClosed(clock.GetUtcNow().UtcDateTime))
                {
                    return Results.Conflict(new { error = ClosedMessage });
                }

                var respondentId = request.RespondentId?.Trim();
                if (respondentId is { Length: > MaxRespondentIdLength })
                {
                    return Results.BadRequest(new { error = $"respondentId must be at most {MaxRespondentIdLength} characters." });
                }
                if (!string.IsNullOrEmpty(respondentId) && responses.HasAnswered(surveyId, respondentId))
                {
                    return Results.Conflict(new { error = AlreadyAnsweredMessage });
                }

                if (!TryReadAnswers(request.Answers, out var answers, out var formatErrors))
                {
                    return Results.ValidationProblem(formatErrors);
                }

                var questions = SurveyEndpoints.LoadQuestions(survey, questionRepository);
                var result = validator.Validate(questions, answers);
                if (!result.IsValid)
                {
                    return Results.ValidationProblem(result.Errors);
                }

                var response = responses.Insert(new SurveyResponse
                {
                    Id = Guid.NewGuid(),
                    SurveyId = surveyId,
                    SubmittedAt = DateTime.UtcNow,
                    SubmittedBy = user.IsSignedIn ? user.Username : null,
                    RespondentId = string.IsNullOrEmpty(respondentId) ? null : respondentId,
                    Answers = result.Answers
                });

                return Results.Created($"/api/surveys/{surveyId}/responses/{response.Id}", response);
            })
            .WithName("SubmitResponse")
            .RequireRateLimiting(SubmitRateLimit)
            .Produces<SurveyResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict)
            .Produces(StatusCodes.Status429TooManyRequests);

            // Lets a survey page say straight away that this browser already answered.
            group.MapGet("/answered", (Guid surveyId, string? respondentId, IResponseRepository responses) =>
                Results.Ok(new { answered = !string.IsNullOrWhiteSpace(respondentId) && responses.HasAnswered(surveyId, respondentId.Trim()) }))
            .WithName("HasAnswered")
            .Produces(StatusCodes.Status200OK);

            group.MapGet("/responses", (Guid surveyId, ClaimsPrincipal principal, ISurveyRepository surveys,
                IResponseRepository responses) =>
            {
                var survey = surveys.FindById(surveyId);
                if (survey is null)
                {
                    return Results.NotFound();
                }
                if (!CurrentUser.From(principal).CanManage(survey))
                {
                    return CurrentUser.NotYours("surveys");
                }
                return Results.Ok(responses.FindBySurveyId(surveyId).ToList());
            })
            .WithName("GetResponses")
            .RequireAuthorization(JwtSettings.BuilderPolicy)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces<List<SurveyResponse>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

            group.MapGet("/results", (Guid surveyId, ClaimsPrincipal principal, ISurveyRepository surveys,
                IQuestionRepository questionRepository, IResponseRepository responses) =>
            {
                var survey = surveys.FindById(surveyId);
                if (survey is null)
                {
                    return Results.NotFound();
                }
                if (!CurrentUser.From(principal).CanManage(survey))
                {
                    return CurrentUser.NotYours("surveys");
                }

                var questions = SurveyEndpoints.LoadQuestions(survey, questionRepository);
                var stored = responses.FindBySurveyId(surveyId).ToList();
                return Results.Ok(SurveyResultsBuilder.Build(survey, questions, stored));
            })
            .WithName("GetSurveyResults")
            .RequireAuthorization(JwtSettings.BuilderPolicy)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces<SurveyResults>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

            group.MapGet("/responses/export", (Guid surveyId, ClaimsPrincipal principal, ISurveyRepository surveys,
                IQuestionRepository questionRepository, IResponseRepository responses) =>
            {
                var survey = surveys.FindById(surveyId);
                if (survey is null)
                {
                    return Results.NotFound();
                }
                if (!CurrentUser.From(principal).CanManage(survey))
                {
                    return CurrentUser.NotYours("surveys");
                }

                var questions = SurveyEndpoints.LoadQuestions(survey, questionRepository);
                var csv = CsvExporter.Export(questions, responses.FindBySurveyId(surveyId));
                return Results.File(Encoding.UTF8.GetBytes(csv), "text/csv", $"{FileNameFor(survey.Title)}-responses.csv");
            })
            .WithName("ExportResponses")
            .RequireAuthorization(JwtSettings.BuilderPolicy)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status200OK, contentType: "text/csv")
            .Produces(StatusCodes.Status404NotFound);
        }

        /// <summary>
        /// Converts the loosely typed JSON answers into lists of strings.
        /// </summary>
        public static bool TryReadAnswers(
            Dictionary<string, JsonElement>? raw,
            out Dictionary<string, List<string>> answers,
            out Dictionary<string, string[]> errors)
        {
            answers = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            errors = new Dictionary<string, string[]>();

            foreach (var (key, element) in raw ?? [])
            {
                var values = new List<string>();
                var items = element.ValueKind == JsonValueKind.Array ? element.EnumerateArray().ToList() : [element];

                foreach (var item in items)
                {
                    switch (item.ValueKind)
                    {
                        case JsonValueKind.String:
                            values.Add(item.GetString()!);
                            break;
                        case JsonValueKind.Number:
                            values.Add(item.GetRawText());
                            break;
                        case JsonValueKind.True:
                            values.Add("true");
                            break;
                        case JsonValueKind.False:
                            values.Add("false");
                            break;
                        case JsonValueKind.Null:
                        case JsonValueKind.Undefined:
                            break;
                        default:
                            errors[key] = ["Answers must be text, numbers, true/false, or a list of those."];
                            break;
                    }
                }

                answers[key] = values;
            }

            return errors.Count == 0;
        }

        private static string FileNameFor(string title)
        {
            var slug = new string(title.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray());
            slug = string.Join('-', slug.Split('-', StringSplitOptions.RemoveEmptyEntries));
            return slug.Length == 0 ? "survey" : slug;
        }
    }
}
