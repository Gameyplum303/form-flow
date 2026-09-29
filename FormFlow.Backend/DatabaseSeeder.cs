using LiteDB;
using System.Text.Json;
using FormFlow.Data.Models;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace FormFlow.Backend
{
    public class DatabaseSeeder
    {
        private readonly ILiteDatabase _dbContext;
        private readonly IWebHostEnvironment _env;

        private readonly IConfiguration? _config;

        public DatabaseSeeder(ILiteDatabase dbContext, IWebHostEnvironment env, IConfiguration? config = null)
        {
            Repositories.LiteDbMappings.EnsureBuilt();
            _dbContext = dbContext;
            _env = env;
            _config = config;
        }

        /// <summary>
        /// Seeds sample questions into an empty database and, unless disabled with
        /// SeedData:DemoSurvey=false, a demo survey that uses them.
        /// </summary>
        public void Seed()
        {
            var questions = _dbContext.GetCollection<QuestionDefinition>("questions");
            SeedFromJson(questions);

            if (_config?.GetValue("SeedData:DemoSurvey", true) ?? true)
            {
                SeedDemoSurvey(questions, _dbContext.GetCollection<SurveyDefinition>("surveys"));
            }
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
                Title = "Student Experience Survey",
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