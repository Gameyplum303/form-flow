using System.Security.Cryptography;
using FormFlow.Data.Models;
using FormFlow.Data.Services;
using LiteDB;

namespace FormFlow.Backend.Repositories
{
    public class SurveyRepository : ISurveyRepository
    {
        public const string CollectionName = "surveys";

        private readonly ILiteCollection<SurveyDefinition> _surveys;

        public SurveyRepository(ILiteDatabase db)
        {
            LiteDbMappings.EnsureBuilt();
            _surveys = db.GetCollection<SurveyDefinition>(CollectionName);

            _surveys.EnsureIndex(s => s.Id, true);

            // Not unique: surveys stored before sharing existed have no code until AssignMissingShareCodes runs.
            _surveys.EnsureIndex(s => s.ShareCode);
        }

        // Codes are stored lowercase. Normalizing also drops the spaces and dashes people type.
        public SurveyDefinition? FindByShareCode(string code) =>
            ShareCodes.Normalize(code) is not { Length: > 0 } normalized ? null
            : _surveys.FindOne(s => s.ShareCode == normalized) is { IsTemplate: false } survey ? survey : null;

        public string NewShareCode()
        {
            while (true)
            {
                var code = RandomNumberGenerator.GetString(ShareCodes.Alphabet, ShareCodes.Length);
                if (FindByShareCode(code) is null)
                {
                    return code;
                }
            }
        }

        public int AssignMissingShareCodes()
        {
            // Templates can't be shared, so they have no code.
            var missing = _surveys.Find(s => s.ShareCode == null).Where(s => !s.IsTemplate).ToList();
            foreach (var survey in missing)
            {
                survey.ShareCode = NewShareCode();
                _surveys.Update(survey);
            }
            return missing.Count;
        }

        public SurveyDefinition Insert(SurveyDefinition survey)
        {
            _surveys.Insert(survey);
            return survey;
        }

        public SurveyDefinition? FindById(Guid id) =>
            _surveys.FindById(id) is { IsTemplate: false } survey ? survey : null;

        // Surveys stored before templates existed have no IsTemplate field, so filter in memory.
        public IEnumerable<SurveyDefinition> FindAll() => _surveys.FindAll().Where(s => !s.IsTemplate);

        public IEnumerable<SurveyDefinition> FindTemplates() => _surveys.FindAll().Where(s => s.IsTemplate);

        public SurveyDefinition? FindTemplate(Guid id) =>
            _surveys.FindById(id) is { IsTemplate: true } template ? template : null;

        public IEnumerable<SurveyDefinition> FindByQuestionId(Guid questionId)
        {
            // LiteDB cannot index into a list of Guids, and survey counts are small,
            // so filter in memory.
            return _surveys.FindAll().Where(s => s.QuestionIds.Contains(questionId));
        }

        public bool Update(SurveyDefinition survey)
        {
            return _surveys.Update(survey);
        }

        public bool Delete(Guid id)
        {
            return _surveys.Delete(id);
        }
    }
}
