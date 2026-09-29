using FormFlow.Data.Models;

namespace FormFlow.Backend.Repositories
{
    public interface ISurveyRepository
    {
        SurveyDefinition Insert(SurveyDefinition survey);
        SurveyDefinition? FindById(Guid id);
        IEnumerable<SurveyDefinition> FindAll();

        /// <summary>Surveys that include the given question.</summary>
        IEnumerable<SurveyDefinition> FindByQuestionId(Guid questionId);

        /// <summary>Replaces the stored survey. Returns false when it does not exist.</summary>
        bool Update(SurveyDefinition survey);

        /// <summary>Deletes the survey. Returns false when it does not exist.</summary>
        bool Delete(Guid id);
    }
}
