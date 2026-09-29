using FormFlow.Backend.Auth;
using FormFlow.Backend.Repositories;
using FormFlow.Data.Models;
using FormFlow.Data.Services;

namespace FormFlow.Backend.Endpoints
{
    public static class QuestionEndpoints
    {
        public static void MapQuestionEndpoints(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("/api/questions").WithTags("Questions");

            group.MapGet("/{id}", (string id, IQuestionRepository repository) =>
            {
                if (string.IsNullOrWhiteSpace(id) || !Guid.TryParse(id, out var parsedId))
                {
                    return Results.BadRequest(new
                    {
                        error = "Invalid question id. Provide a non-empty GUID value."
                    });
                }

                var question = repository.FindById(parsedId);

                if (question is null)
                {
                    return Results.NotFound();
                }

                return Results.Json(question);
            })
            .WithName("GetQuestionById")
            .Produces<QuestionDefinition>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

            group.MapGet("", (IQuestionRepository repository) =>
            {
                return Results.Json(repository.FindAll().ToList());
            })
            .WithName("GetAllQuestions")
            .Produces<List<QuestionDefinition>>(StatusCodes.Status200OK);

            group.MapPost("", (NewQuestion newQuestion, IQuestionRepository repository, QuestionValidator validator) =>
            {
                var question = ToDefinition(Guid.NewGuid(), newQuestion);

                var errors = Validate(question, repository, validator);
                if (errors.Count > 0)
                {
                    return Results.BadRequest(new { errors });
                }

                // Check if key is unique
                var existingQuestion = repository.FindOne(q => q.Key == question.Key);
                if (existingQuestion != null)
                {
                    return Results.Conflict($"A question with key '{question.Key}' already exists");
                }

                // Insert using repository
                try
                {
                    repository.Insert(question);
                    return Results.Created($"/api/questions/{question.Id}", question);
                }
                catch (ArgumentNullException)
                {
                    return Results.BadRequest(new { errors = new[] { "Invalid question data provided" } });
                }
                catch (Exception)
                {
                    return Results.StatusCode(StatusCodes.Status500InternalServerError);
                }
            })
            .WithName("CreateQuestion")
            .RequireAuthorization(JwtSettings.AdminPolicy)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces<QuestionDefinition>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status409Conflict);

            group.MapPut("/{id:guid}", (Guid id, NewQuestion update, IQuestionRepository repository,
                ISurveyRepository surveys, QuestionValidator validator) =>
            {
                var existing = repository.FindById(id);
                if (existing is null)
                {
                    return Results.NotFound();
                }

                var question = ToDefinition(id, update);

                var errors = Validate(question, repository, validator);
                if (errors.Count > 0)
                {
                    return Results.BadRequest(new { errors });
                }

                var duplicate = repository.FindOne(q => q.Key == question.Key && q.Id != id);
                if (duplicate is not null)
                {
                    return Results.Conflict(new { error = $"A question with key '{question.Key}' already exists" });
                }

                // Stored answers and other questions' visibility rules refer to questions by key,
                // so a key that is already in use must stay stable.
                if (question.Key != existing.Key && IsInUse(existing, repository, surveys))
                {
                    return Results.Conflict(new
                    {
                        error = $"The key '{existing.Key}' cannot change because surveys or other questions use it"
                    });
                }

                repository.Update(question);
                return Results.Ok(question);
            })
            .WithName("UpdateQuestion")
            .RequireAuthorization(JwtSettings.AdminPolicy)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces<QuestionDefinition>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);

            group.MapDelete("/{id:guid}", (Guid id, IQuestionRepository repository, ISurveyRepository surveys) =>
            {
                var existing = repository.FindById(id);
                if (existing is null)
                {
                    return Results.NotFound();
                }

                var usedBy = surveys.FindByQuestionId(id).Select(s => s.Title).ToList();
                if (usedBy.Count > 0)
                {
                    return Results.Conflict(new
                    {
                        error = $"This question is used by: {string.Join(", ", usedBy)}. Remove it from those surveys first."
                    });
                }

                var dependents = repository.FindAll()
                    .Where(q => q.VisibleIf?.Key == existing.Key)
                    .Select(q => q.Key)
                    .ToList();
                if (dependents.Count > 0)
                {
                    return Results.Conflict(new
                    {
                        error = $"These questions only show based on this one: {string.Join(", ", dependents)}"
                    });
                }

                repository.Delete(id);
                return Results.NoContent();
            })
            .WithName("DeleteQuestion")
            .RequireAuthorization(JwtSettings.AdminPolicy)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);
        }

        private static QuestionDefinition ToDefinition(Guid id, NewQuestion source) => new()
        {
            Id = id,
            Key = source.Key,
            Label = source.Label,
            Type = source.Type,
            Required = source.Required,
            Placeholder = source.Placeholder,
            DefaultValue = source.DefaultValue,
            HelpText = source.HelpText,
            Options = source.Options ?? [],
            VisibleIf = source.VisibleIf,
            ValidationConfigs = string.IsNullOrWhiteSpace(source.ValidationConfigs) ? null : source.ValidationConfigs
        };

        private static List<string?> Validate(QuestionDefinition question, IQuestionRepository repository, QuestionValidator validator)
        {
            var errors = validator.Validate(question).Errors.Select(e => e.Message).ToList();
            if (errors.Count > 0 || question.VisibleIf is null)
            {
                return errors;
            }

            // The rule compares against true/false, so the question it depends on must be a yes/no question.
            var controller = repository.FindOne(q => q.Key == question.VisibleIf.Key);
            if (controller is null)
            {
                errors.Add($"Visibility rule refers to unknown question '{question.VisibleIf.Key}'");
            }
            else if (!string.Equals(controller.Type, QuestionTypes.YesNo, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"Visibility rules can only depend on yes/no questions; '{controller.Key}' is '{controller.Type}'");
            }
            return errors;
        }

        private static bool IsInUse(QuestionDefinition question, IQuestionRepository repository, ISurveyRepository surveys) =>
            surveys.FindByQuestionId(question.Id).Any()
            || repository.FindAll().Any(q => q.VisibleIf?.Key == question.Key);
    }
}
