using System.Text.Json;
using FormFlow.Backend.Repositories;
using FormFlow.Backend.Services;
using FormFlow.Data.Models;
using FormFlow.Data.Services;
using LiteDB;
using Microsoft.Extensions.Logging.Abstractions;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace FormFlow.Backend
{
    public class DatabaseSeeder
    {
        private readonly ILiteDatabase _dbContext;
        private readonly IWebHostEnvironment _env;

        private readonly IConfiguration? _config;
        private readonly ResponseValidator? _validator;
        private readonly TimeProvider _clock;
        private readonly ILogger<DatabaseSeeder> _logger;

        // The seed file, read on first use and kept for the rest of seeding.
        private List<QuestionDefinition>? _seedQuestions;

        public const string DemoSurveyTitle = "Student Experience Survey";

        public DatabaseSeeder(ILiteDatabase dbContext, IWebHostEnvironment env, IConfiguration? config = null,
            ResponseValidator? validator = null, TimeProvider? clock = null, ILogger<DatabaseSeeder>? logger = null)
        {
            LiteDbMappings.EnsureBuilt();
            _dbContext = dbContext;
            _env = env;
            _config = config;
            _validator = validator;
            _clock = clock ?? TimeProvider.System;
            _logger = logger ?? NullLogger<DatabaseSeeder>.Instance;
        }

        /// <summary>
        /// Seeds sample questions into an empty database and, unless disabled with
        /// SeedData:DemoSurvey=false, a demo survey that uses them. With SeedData:SampleResponses
        /// set, the demo survey also gets that many made-up responses, so a public demo has results
        /// to explore.
        /// </summary>
        public void Seed()
        {
            var questions = _dbContext.GetCollection<QuestionDefinition>(QuestionRepository.CollectionName);
            SeedFromJson(questions);

            if (_config?.GetValue("SeedData:DemoSurvey", true) ?? true)
            {
                var surveys = _dbContext.GetCollection<SurveyDefinition>(SurveyRepository.CollectionName);
                SeedDemoSurvey(questions, surveys);
                SeedSampleResponses(questions, surveys, _config?.GetValue("SeedData:SampleResponses", 0) ?? 0);
            }
        }

        /// <summary>Adds made-up responses to the demo survey, if it has none yet.</summary>
        public void SeedSampleResponses(ILiteCollection<QuestionDefinition> questions, ILiteCollection<SurveyDefinition> surveys, int count)
        {
            if (count <= 0 || _validator is null)
            {
                return;
            }
            var responses = _dbContext.GetCollection<SurveyResponse>(ResponseRepository.CollectionName);
            var survey = surveys.FindOne(s => s.Title == DemoSurveyTitle);
            if (survey is null || responses.Exists(r => r.SurveyId == survey.Id))
            {
                return;
            }

            var byId = questions.FindAll().ToDictionary(q => q.Id);
            var ordered = survey.QuestionIds.Where(byId.ContainsKey).Select(id => byId[id]).ToList();
            var sample = SampleResponseGenerator.Generate(survey, ordered, _validator, count, _clock.GetUtcNow().UtcDateTime);
            responses.InsertBulk(sample);
            _logger.LogInformation("Seeded {Count} sample responses to \"{Title}\".", sample.Count, survey.Title);
        }

        public void SeedDemoSurvey(ILiteCollection<QuestionDefinition> questions, ILiteCollection<SurveyDefinition> surveys)
        {
            if (surveys.Count() > 0)
            {
                return;
            }

            // Keep the order of the seed file so the conditional question follows the one it depends on.
            var seedOrder = ReadSeedFile()?.Select(q => q.Key).ToList() ?? [];
            var questionIds = questions.FindAll()
                .OrderBy(q => seedOrder.IndexOf(q.Key) is var i && i >= 0 ? i : int.MaxValue)
                .Select(q => q.Id)
                .ToList();
            if (questionIds.Count == 0)
            {
                return;
            }

            surveys.Insert(new SurveyDefinition
            {
                Id = Guid.NewGuid(),
                Title = DemoSurveyTitle,
                Description = "A demo survey built from the sample questions. Answer yes to \"Are you currently a student?\" to see the campus question appear.",
                QuestionIds = questionIds,
                CreatedAt = _clock.GetUtcNow().UtcDateTime
            });
        }
        public void SeedFromJson(ILiteCollection<QuestionDefinition> collection)
        {

            if (collection.Count() == 0)
            {
                var questionDefinitions = ReadSeedFile()
                    ?? throw new FileNotFoundException($"Seed data file not found: {SeedFilePath}");

                if (questionDefinitions.Count == 0)
                {
                    throw new InvalidDataException($"Seed data file is empty or could not be deserialized: {SeedFilePath}");
                }

                foreach (var question in questionDefinitions)
                {
                    if (question.Id == Guid.Empty)
                    {
                        question.Id = Guid.NewGuid();
                    }
                }

                collection.InsertBulk(questionDefinitions);

                _logger.LogInformation("Seeded {Count} sample questions.", questionDefinitions.Count);

            }
        }

        private string SeedFilePath => Path.Combine(_env.ContentRootPath, "SeedData", "questions.json");

        /// <summary>The sample questions in SeedData/questions.json, or null when the file is missing.</summary>
        private List<QuestionDefinition>? ReadSeedFile()
        {
            if (_seedQuestions is null && File.Exists(SeedFilePath))
            {
                _seedQuestions = JsonSerializer.Deserialize<List<QuestionDefinition>>(File.ReadAllText(SeedFilePath),
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
            }
            return _seedQuestions;
        }
    }
}