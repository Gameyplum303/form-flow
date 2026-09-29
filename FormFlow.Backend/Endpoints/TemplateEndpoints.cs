using System.Security.Claims;
using FormFlow.Backend.Auth;
using FormFlow.Backend.Repositories;
using FormFlow.Data.Models;

namespace FormFlow.Backend.Endpoints
{
    /// <summary>
    /// Survey templates: ready-made surveys, seeded from SeedData/templates.json, that builders copy
    /// into a new draft. Templates are read-only and never answered or shared themselves.
    /// </summary>
    public static class TemplateEndpoints
    {
        public static void MapTemplateEndpoints(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("/api/templates").WithTags("Templates").RequireAuthorization(JwtSettings.BuilderPolicy);

            group.MapGet("", (ISurveyRepository repo) =>
                Results.Ok(repo.FindTemplates()
                    .OrderBy(t => t.Title, StringComparer.OrdinalIgnoreCase)
                    .Select(t => new SurveyTemplate
                    {
                        Id = t.Id,
                        Title = t.Title,
                        Description = t.Description,
                        QuestionCount = t.QuestionIds.Count,
                        PageCount = t.PageBreaks.Count + 1,
                    })
                    .ToList()))
            .WithName("GetTemplates")
            .Produces<List<SurveyTemplate>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

            // Starts a new survey from a template: a draft the caller owns, with the template's questions and pages
            group.MapPost("/{id:guid}/use", (Guid id, ClaimsPrincipal principal, ISurveyRepository repo, TimeProvider clock) =>
            {
                if (repo.FindTemplate(id) is not { } template)
                {
                    return Results.NotFound();
                }

                var survey = SurveyEndpoints.DraftCopy(template, template.Title, CurrentUser.From(principal), repo, clock);
                return Results.Created($"/api/surveys/{survey.Id}", survey);
            })
            .WithName("UseTemplate")
            .Produces<SurveyDefinition>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);
        }
    }
}
