using System.Security.Claims;
using FormFlow.Backend.Auth;
using FormFlow.Backend.Repositories;
using FormFlow.Data.Models;
using FormFlow.Data.Services;

namespace FormFlow.Backend.Endpoints
{
    /// <summary>Survey endpoints: the public list, share links, and building and sharing surveys.</summary>
    public static class SurveyEndpoints
    {
        public static void MapSurveyEndpoints(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("/api/surveys").WithTags("Surveys");

            // GET the public list: published, listed surveys that are still open
            group.MapGet("", (ClaimsPrincipal principal, ISurveyRepository repo, TimeProvider clock) =>
            {
                var now = clock.GetUtcNow().UtcDateTime;
                var user = CurrentUser.From(principal);
                return Results.Json(repo.FindAll().Where(s => s.IsOnPublicList(now)).Select(user.ShowOwnerToBuilders).ToList());
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
            group.MapGet("/{id:guid}", (Guid id, ClaimsPrincipal principal, ISurveyRepository repo) =>
            {
                var survey = repo.FindById(id);
                var user = CurrentUser.From(principal);
                if (survey is null || !user.CanOpen(survey))
                {
                    return Results.NotFound();
                }
                return Results.Ok(user.ShowOwnerToBuilders(survey));
            })
            .WithName("GetSurveyById")
            .Produces<SurveyDefinition>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

            // GET the survey's questions, in survey order, so a client can render it in one call
            group.MapGet("/{id:guid}/questions", (Guid id, ClaimsPrincipal principal, ISurveyRepository repo,
                IQuestionRepository questions) =>
            {
                var survey = repo.FindById(id);
                var user = CurrentUser.From(principal);
                if (survey is null || !user.CanOpen(survey))
                {
                    return Results.NotFound();
                }
                return Results.Ok(LoadQuestions(survey, questions).Select(user.ShowOwnerToBuilders).ToList());
            })
            .WithName("GetSurveyQuestions")
            .Produces<List<QuestionDefinition>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

            group.MapPost("", (NewSurvey dto, ClaimsPrincipal principal, ISurveyRepository repo, IQuestionRepository questions,
                TimeProvider clock) =>
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
                    PageBreaks = SurveyPaging.Normalize(dto.QuestionIds, dto.PageBreaks),
                    CreatedAt = clock.GetUtcNow().UtcDateTime,
                    // New surveys stay private until their owner publishes them.
                    Status = SurveyStatuses.Draft,
                    Listed = false,
                    ShareCode = repo.NewShareCode(),
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
                if (CurrentUser.From(principal).CannotManage(existing, "surveys", out var denied))
                {
                    return denied;
                }

                var error = Validate(dto, questions);
                if (error is not null)
                {
                    return Results.BadRequest(new { error });
                }

                existing.Title = dto.Title.Trim();
                existing.Description = dto.Description.Trim();
                existing.QuestionIds = dto.QuestionIds;
                existing.PageBreaks = SurveyPaging.Normalize(dto.QuestionIds, dto.PageBreaks);
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

            // Copy a survey into a new draft the caller owns: their own, any survey for an administrator, or any published one
            group.MapPost("/{id:guid}/duplicate", (Guid id, ClaimsPrincipal principal, ISurveyRepository repo, TimeProvider clock) =>
            {
                var survey = repo.FindById(id);
                var user = CurrentUser.From(principal);
                if (survey is null || !user.CanOpen(survey))
                {
                    return Results.NotFound();
                }

                var copy = DraftCopy(survey, $"Copy of {survey.Title}", user, repo, clock);
                return Results.Created($"/api/surveys/{copy.Id}", copy);
            })
            .WithName("DuplicateSurvey")
            .RequireAuthorization(JwtSettings.BuilderPolicy)
            .Produces<SurveyDefinition>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

            // Publish or unpublish a survey, choose whether it is on the public list, and when it closes
            group.MapPut("/{id:guid}/sharing", (Guid id, SurveySharing sharing, ClaimsPrincipal principal, ISurveyRepository repo) =>
            {
                var existing = repo.FindById(id);
                if (CurrentUser.From(principal).CannotManage(existing, "surveys", out var denied))
                {
                    return denied;
                }
                if (!SurveyStatuses.IsKnown(sharing.Status))
                {
                    return Results.BadRequest(new { error = $"Status must be \"{SurveyStatuses.Draft}\" or \"{SurveyStatuses.Published}\"." });
                }

                existing.Status = sharing.Status;
                existing.Listed = sharing.Listed;
                existing.ClosesAt = sharing.ClosesAt?.ToUniversalTime();
                repo.Update(existing);
                return Results.Ok(existing);
            })
            .WithName("UpdateSurveySharing")
            .RequireAuthorization(JwtSettings.BuilderPolicy)
            .Produces<SurveyDefinition>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

            // Open a survey from its share link
            app.MapGet("/api/share/{code}", (string code, ClaimsPrincipal principal, ISurveyRepository repo) =>
            {
                var user = CurrentUser.From(principal);
                return repo.FindByShareCode(code) is { } survey && user.CanOpen(survey)
                    ? Results.Ok(user.ShowOwnerToBuilders(survey))
                    : Results.NotFound();
            })
            .WithTags("Surveys")
            .WithName("GetSurveyByShareCode")
            .Produces<SurveyDefinition>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

            group.MapDelete("/{id:guid}", (Guid id, ClaimsPrincipal principal, ISurveyRepository repo,
                IResponseRepository responses) =>
            {
                var existing = repo.FindById(id);
                if (CurrentUser.From(principal).CannotManage(existing, "surveys", out var denied))
                {
                    return denied;
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
        /// Stores a copy of a survey or template as a new draft owned by the caller: the same questions and
        /// pages, with a new share code, no responses, and no close date.
        /// </summary>
        public static SurveyDefinition DraftCopy(SurveyDefinition source, string title, CurrentUser user,
            ISurveyRepository repo, TimeProvider clock)
        {
            var copy = new SurveyDefinition
            {
                Id = Guid.NewGuid(),
                Title = title,
                Description = source.Description,
                QuestionIds = source.QuestionIds.ToList(),
                PageBreaks = SurveyPaging.Normalize(source.QuestionIds, source.PageBreaks),
                CreatedAt = clock.GetUtcNow().UtcDateTime,
                Status = SurveyStatuses.Draft,
                Listed = false,
                ShareCode = repo.NewShareCode(),
            };
            user.Own(copy);
            return repo.Insert(copy);
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
