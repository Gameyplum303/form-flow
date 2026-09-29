using LiteDB;
using FormFlow.Data.Models;

namespace FormFlow.Backend.Repositories
{
    public class SurveyRepository : ISurveyRepository
    {
        public const string CollectionName = "surveys";

        private readonly ILiteCollection<SurveyDefinition> _surveys;

        public SurveyRepository(ILiteDatabase db)
        {
            _surveys = db.GetCollection<SurveyDefinition>(CollectionName);

            _surveys.EnsureIndex(s => s.Id, true);
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
