using System.Security.Claims;
using FormFlow.Data.Models;
using FormFlow.Backend.Auth;
using FormFlow.Backend.Repositories;

namespace FormFlow.Backend.Endpoints
{
    public static class SurveyEndpoints
    {
        public static void MapSurveyEndpoints(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("/api/surveys").WithTags("Surveys");

            // GET All Surveys
            group.MapGet("", (ISurveyRepository repo) =>
            {
                var surveys = repo.FindAll().ToList();
                return Results.Json(surveys);
            })
            .WithName("GetAllSurveys")
            .Produces<List<SurveyDefinition>>(StatusCodes.Status200OK);

            // GET the surveys the caller manages: all of them for an administrator, their own for a professor
            group.MapGet("/managed", (ClaimsPrincipal principal, ISurveyRepository repo) =>
            {
                var user = CurrentUser.From(principal);
                return Results.Ok(repo.FindAll().Where(user.CanManage).OrderByDescending(s => s.CreatedAt).ToList());
            })
            .WithName("GetManagedSurveys")
            .RequireAuthorization(JwtSettings.BuilderPolicy)
            .Produces<List<SurveyDefinition>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

            // GET survey by id
            group.MapGet("/{id}", (string id, ISurveyRepository repo) =>
            {
                if (!Guid.TryParse(id, out var parsedId))
                {
                    return Results.BadRequest(new
                    {
                        error = "Invalid survey id. Must be a GUID."
                    });
                }

                var survey = repo.FindById(parsedId);

                if (survey is null)
                {
                    return Results.NotFound();
                }
                return Results.Ok(survey);
            })
            .WithName("GetSurveyById")
            .Produces<SurveyDefinition>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

            // GET the survey's questions, in survey order, so a client can render it in one call
            group.MapGet("/{id:guid}/questions", (Guid id, ISurveyRepository repo, IQuestionRepository questions) =>
            {
                var survey = repo.FindById(id);
                if (survey is null)
                {
                    return Results.NotFound();
                }
                return Results.Ok(LoadQuestions(survey, questions));
            })
            .WithName("GetSurveyQuestions")
            .Produces<List<QuestionDefinition>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

            group.MapPost("", (NewSurvey dto, ClaimsPrincipal principal, ISurveyRepository repo, IQuestionRepository questions) =>
            {
                var error = Validate(dto, questions);
                if (error is not null)
                {
                    return Results.BadRequest(new { error });
                }

                var survey = new SurveyDefinition
                {
                    Id = Guid.NewGuid(),
                    Title = dto.Title.Trim(),
                    Description = dto.Description.Trim(),
                    QuestionIds = dto.QuestionIds,
                    CreatedAt = DateTime.UtcNow
                };
                CurrentUser.From(principal).Own(survey);

                repo.Insert(survey);

                return Results.Created($"/api/surveys/{survey.Id}", survey);
            })
            .WithName("CreateSurvey")
            .RequireAuthorization(JwtSettings.BuilderPolicy)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces<SurveyDefinition>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest);

            group.MapPut("/{id:guid}", (Guid id, NewSurvey dto, ClaimsPrincipal principal, ISurveyRepository repo,
                IQuestionRepository questions) =>
            {
                var existing = repo.FindById(id);
                if (existing is null)
                {
                    return Results.NotFound();
                }
                if (!CurrentUser.From(principal).CanManage(existing))
                {
                    return CurrentUser.NotYours("surveys");
                }

                var error = Validate(dto, questions);
                if (error is not null)
                {
                    return Results.BadRequest(new { error });
                }

                existing.Title = dto.Title.Trim();
                existing.Description = dto.Description.Trim();
                existing.QuestionIds = dto.QuestionIds;
                repo.Update(existing);

                return Results.Ok(existing);
            })
            .WithName("UpdateSurvey")
            .RequireAuthorization(JwtSettings.BuilderPolicy)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces<SurveyDefinition>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

            group.MapDelete("/{id:guid}", (Guid id, ClaimsPrincipal principal, ISurveyRepository repo,
                IResponseRepository responses) =>
            {
                var existing = repo.FindById(id);
                if (existing is null)
                {
                    return Results.NotFound();
                }
                if (!CurrentUser.From(principal).CanManage(existing))
                {
                    return CurrentUser.NotYours("surveys");
                }

                repo.Delete(id);

                // Responses are meaningless without their survey.
                responses.DeleteBySurveyId(id);
                return Results.NoContent();
            })
            .WithName("DeleteSurvey")
            .RequireAuthorization(JwtSettings.BuilderPolicy)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);
        }

        /// <summary>
        /// Loads a survey's questions in survey order, skipping any that no longer exist.
        /// </summary>
        public static List<QuestionDefinition> LoadQuestions(SurveyDefinition survey, IQuestionRepository questions) =>
            survey.QuestionIds
                .Select(questions.FindById)
                .OfType<QuestionDefinition>()
                .ToList();

        private static string? Validate(NewSurvey dto, IQuestionRepository questions)
        {
            if (string.IsNullOrWhiteSpace(dto.Title))
            {
                return "Title is required.";
            }

            if (string.IsNullOrWhiteSpace(dto.Description))
            {
                return "Description is required.";
            }

            if (dto.QuestionIds == null || dto.QuestionIds.Count == 0)
            {
                return "At least one question is required.";
            }

            if (dto.QuestionIds.Distinct().Count() != dto.QuestionIds.Count)
            {
                return "A question can only appear once in a survey.";
            }

            var missing = dto.QuestionIds.Where(q => questions.FindById(q) is null).ToList();
            if (missing.Count > 0)
            {
                return $"Unknown question ids: {string.Join(", ", missing)}";
            }

            return null;
        }
    }
}
