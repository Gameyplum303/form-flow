using LiteDB;
using System.Text.Json;
using FormFlow.Backend.Services;
using FormFlow.Data.Models;
using FormFlow.Data.Services;
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

        public const string DemoSurveyTitle = "Student Experience Survey";

        public DatabaseSeeder(ILiteDatabase dbContext, IWebHostEnvironment env, IConfiguration? config = null,
            ResponseValidator? validator = null, TimeProvider? clock = null)
        {
            Repositories.LiteDbMappings.EnsureBuilt();
            _dbContext = dbContext;
            _env = env;
            _config = config;
            _validator = validator;
            _clock = clock ?? TimeProvider.System;
        }

        /// <summary>
        /// Seeds sample questions into an empty database and, unless disabled with
        /// SeedData:DemoSurvey=false, a demo survey that uses them. With SeedData:SampleResponses
        /// set, the demo survey also gets that many made-up responses, so a public demo has results
        /// to explore.
        /// </summary>
        public void Seed()
        {
            var questions = _dbContext.GetCollection<QuestionDefinition>("questions");
            SeedFromJson(questions);

            if (_config?.GetValue("SeedData:DemoSurvey", true) ?? true)
            {
                var surveys = _dbContext.GetCollection<SurveyDefinition>("surveys");
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
            var responses = _dbContext.GetCollection<SurveyResponse>(Repositories.ResponseRepository.CollectionName);
            var survey = surveys.FindOne(s => s.Title == DemoSurveyTitle);
            if (survey is null || responses.Exists(r => r.SurveyId == survey.Id))
            {
                return;
            }

            var byId = questions.FindAll().ToDictionary(q => q.Id);
            var ordered = survey.QuestionIds.Where(byId.ContainsKey).Select(id => byId[id]).ToList();
            var sample = SampleResponseGenerator.Generate(survey, ordered, _validator, count, _clock.GetUtcNow().UtcDateTime);
            responses.InsertBulk(sample);
            Console.WriteLine($"Seeded {sample.Count} sample responses to \"{survey.Title}\".");
        }

        public void SeedDemoSurvey(ILiteCollection<QuestionDefinition> questions, ILiteCollection<SurveyDefinition> surveys)
        {
            if (surveys.Count() > 0)
            {
                return;
            }

            // Keep the order of the seed file so the conditional question follows the one it depends on.
            var seedOrder = ReadSeedKeys();
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
                CreatedAt = DateTime.UtcNow
            });
        }
        private List<string> ReadSeedKeys()
        {
            var path = Path.Combine(_env.ContentRootPath, "SeedData", "questions.json");
            if (!File.Exists(path))
            {
                return [];
            }

            var seeded = JsonSerializer.Deserialize<List<QuestionDefinition>>(File.ReadAllText(path),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return seeded?.Select(q => q.Key).ToList() ?? [];
        }

        public void SeedFromJson(ILiteCollection<QuestionDefinition> collection)
        {

            if (collection.Count() == 0)
            {
                var seedDataPath = Path.Combine(_env.ContentRootPath, "SeedData", "questions.json");


                if (!File.Exists(seedDataPath))
                {
                    throw new FileNotFoundException($"Seed data file not found: {seedDataPath}");
                }

                var json = File.ReadAllText(seedDataPath);
                var questionDefinitions = JsonSerializer.Deserialize<List<QuestionDefinition>>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (questionDefinitions == null || questionDefinitions.Count == 0)
                {
                    throw new InvalidDataException($"Seed data file is empty or could not be deserialized: {seedDataPath}");
                }

                foreach (var question in questionDefinitions)
                {
                    if (question.Id == Guid.Empty)
                    {
                        question.Id = Guid.NewGuid();
                    }
                }

                collection.InsertBulk(questionDefinitions);

                Console.WriteLine($"Seeded {questionDefinitions.Count} sample questions.");

            }
        }
    }
}