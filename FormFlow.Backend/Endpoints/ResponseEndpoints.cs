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
    }

    public static class ResponseEndpoints
    {
        public const string SubmitRateLimit = "submissions";

        public static void MapResponseEndpoints(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("/api/surveys/{surveyId:guid}").WithTags("Responses");

            group.MapPost("/responses", (Guid surveyId, SubmitResponseRequest request, ISurveyRepository surveys,
                IQuestionRepository questionRepository, IResponseRepository responses, ResponseValidator validator) =>
            {
                var survey = surveys.FindById(surveyId);
                if (survey is null)
                {
                    return Results.NotFound();
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
                    Answers = result.Answers
                });

                return Results.Created($"/api/surveys/{surveyId}/responses/{response.Id}", response);
            })
            .WithName("SubmitResponse")
            .RequireRateLimiting(SubmitRateLimit)
            .Produces<SurveyResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status429TooManyRequests);

            group.MapGet("/responses", (Guid surveyId, ISurveyRepository surveys, IResponseRepository responses) =>
            {
                if (surveys.FindById(surveyId) is null)
                {
                    return Results.NotFound();
                }
                return Results.Ok(responses.FindBySurveyId(surveyId).ToList());
            })
            .WithName("GetResponses")
            .RequireAuthorization(JwtSettings.AdminPolicy)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces<List<SurveyResponse>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

            group.MapGet("/results", (Guid surveyId, ISurveyRepository surveys, IQuestionRepository questionRepository,
                IResponseRepository responses) =>
            {
                var survey = surveys.FindById(surveyId);
                if (survey is null)
                {
                    return Results.NotFound();
                }

                var questions = SurveyEndpoints.LoadQuestions(survey, questionRepository);
                var stored = responses.FindBySurveyId(surveyId).ToList();
                return Results.Ok(SurveyResultsBuilder.Build(survey, questions, stored));
            })
            .WithName("GetSurveyResults")
            .RequireAuthorization(JwtSettings.AdminPolicy)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces<SurveyResults>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

            group.MapGet("/responses/export", (Guid surveyId, ISurveyRepository surveys,
                IQuestionRepository questionRepository, IResponseRepository responses) =>
            {
                var survey = surveys.FindById(surveyId);
                if (survey is null)
                {
                    return Results.NotFound();
                }

                var questions = SurveyEndpoints.LoadQuestions(survey, questionRepository);
                var csv = CsvExporter.Export(questions, responses.FindBySurveyId(surveyId));
                return Results.File(Encoding.UTF8.GetBytes(csv), "text/csv", $"{FileNameFor(survey.Title)}-responses.csv");
            })
            .WithName("ExportResponses")
            .RequireAuthorization(JwtSettings.AdminPolicy)
            .Produces(StatusCodes.Status401Unauthorized)
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
