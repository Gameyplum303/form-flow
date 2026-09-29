using System.Security.Cryptography;
using FormFlow.Data.Models;
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

        // No 0/o, 1/l/i, so a code read aloud or copied by hand comes out right.
        private const string ShareCodeAlphabet = "23456789abcdefghjkmnpqrstuvwxyz";
        private const int ShareCodeLength = 8;

        public SurveyDefinition? FindByShareCode(string code) =>
            string.IsNullOrWhiteSpace(code) ? null : _surveys.FindOne(s => s.ShareCode == code.Trim().ToLowerInvariant());

        public string NewShareCode()
        {
            while (true)
            {
                var code = RandomNumberGenerator.GetString(ShareCodeAlphabet, ShareCodeLength);
                if (FindByShareCode(code) is null)
                {
                    return code;
                }
            }
        }

        public int AssignMissingShareCodes()
        {
            var missing = _surveys.Find(s => s.ShareCode == null).ToList();
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

        public SurveyDefinition? FindById(Guid id)
        {
            return _surveys.FindById(id);
        }

        public IEnumerable<SurveyDefinition> FindAll()
        {
            return _surveys.FindAll();
        }

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
