using FormFlow.Data.Models;

namespace FormFlow.Backend.Repositories
{
    public interface ISurveyRepository
    {
        SurveyDefinition Insert(SurveyDefinition survey);

        /// <summary>The survey, or null when there is none. Templates aren't surveys here; see <see cref="FindTemplate"/>.</summary>
        SurveyDefinition? FindById(Guid id);

        /// <summary>Every survey, without the templates.</summary>
        IEnumerable<SurveyDefinition> FindAll();

        /// <summary>Finds a survey (never a template) by the code in its share link.</summary>
        SurveyDefinition? FindByShareCode(string code);

        /// <summary>The ready-made surveys builders start from.</summary>
        IEnumerable<SurveyDefinition> FindTemplates();

        /// <summary>The template, or null when there is none.</summary>
        SurveyDefinition? FindTemplate(Guid id);

        /// <summary>A new share code that no survey uses yet.</summary>
        string NewShareCode();

        /// <summary>Gives surveys stored before sharing existed a share code. Returns how many it changed.</summary>
        int AssignMissingShareCodes();

        /// <summary>Surveys and templates that include the given question.</summary>
        IEnumerable<SurveyDefinition> FindByQuestionId(Guid questionId);

        /// <summary>Replaces the stored survey. Returns false when it does not exist.</summary>
        bool Update(SurveyDefinition survey);

        /// <summary>Deletes the survey. Returns false when it does not exist.</summary>
        bool Delete(Guid id);
    }
}
