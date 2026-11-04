using System.Security.Claims;
using FormFlow.Backend.Auth;
using FormFlow.Backend.Live;
using FormFlow.Backend.Repositories;
using FormFlow.Backend.Services;
using FormFlow.Data.Models;
using Microsoft.Extensions.Options;

namespace FormFlow.Backend.Endpoints
{
    /// <summary>
    /// Results as they come in: the live stream behind the presenter view, and the summary respondents
    /// see after answering a survey that shares its results.
    /// </summary>
    public static class LiveResultsEndpoints
    {
        public static void MapLiveResultsEndpoints(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("/api/surveys/{surveyId:guid}/results").WithTags("Responses");

            // A server-sent events stream of the survey's results, for the builders who manage it.
            group.MapGet("/live", (Guid surveyId, ClaimsPrincipal principal, ISurveyRepository surveys,
                IQuestionRepository questionRepository, IResponseRepository responses, ILiveResultsPublisher publisher,
                IOptions<LiveResultsOptions> options, IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions> json, TimeProvider clock) =>
            {
                var survey = surveys.FindById(surveyId);
                if (CurrentUser.From(principal).CannotManage(survey, "surveys", out var denied))
                {
                    return denied;
                }

                // Read afresh for every update, so edits to the survey's questions show up too.
                SurveyResults? Current() => surveys.FindById(surveyId) is { } latest
                    ? SurveyResultsBuilder.Build(latest, SurveyEndpoints.LoadQuestions(latest, questionRepository),
                        responses.FindBySurveyId(surveyId).ToList(), now: clock.GetUtcNow().UtcDateTime)
                    : null;

                return new LiveResultsStream(surveyId, Current, publisher, options.Value, clock, json.Value.SerializerOptions);
            })
            .WithName("StreamLiveResults")
            .WithSummary("Streams the survey's results as server-sent events")
            .WithDescription("Sends a `results` event with the current results, then another whenever a response arrives " +
                "(at most a few per second), and a `: keep-alive` comment when nothing happens for a while.")
            .RequireAuthorization(JwtSettings.BuilderPolicy)
            .Produces<SurveyResults>(StatusCodes.Status200OK, LiveResultsStream.ContentType)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

            // Counts for the questions with fixed answers, for anyone, once the owner shares the results.
            group.MapGet("/public", (Guid surveyId, ISurveyRepository surveys, IQuestionRepository questionRepository,
                IResponseRepository responses) =>
            {
                var survey = surveys.FindById(surveyId);
                if (survey is not { ShowResultsToRespondents: true } || !survey.IsPublished())
                {
                    return Results.NotFound();
                }

                var questions = SurveyEndpoints.LoadQuestions(survey, questionRepository);
                return Results.Ok(RespondentResultsBuilder.Build(survey, questions, responses.FindBySurveyId(surveyId).ToList()));
            })
            .WithName("GetRespondentResults")
            .WithSummary("The results summary respondents see after answering")
            .Produces<RespondentResults>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);
        }
    }
}
