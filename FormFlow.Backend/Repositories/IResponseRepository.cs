using FormFlow.Data.Models;

namespace FormFlow.Backend.Repositories
{
    public interface IResponseRepository
    {
        SurveyResponse Insert(SurveyResponse response);

        /// <summary>All responses to a survey, oldest first.</summary>
        IEnumerable<SurveyResponse> FindBySurveyId(Guid surveyId);

        int CountBySurveyId(Guid surveyId);

        /// <summary>Whether the browser with this respondent id already answered the survey.</summary>
        bool HasAnswered(Guid surveyId, string respondentId);

        /// <summary>Deletes every response to a survey and returns how many were removed.</summary>
        int DeleteBySurveyId(Guid surveyId);
    }
}
