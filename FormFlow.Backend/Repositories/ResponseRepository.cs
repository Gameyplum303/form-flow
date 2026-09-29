using FormFlow.Data.Models;
using LiteDB;

namespace FormFlow.Backend.Repositories
{
    public class ResponseRepository : IResponseRepository
    {
        public const string CollectionName = "responses";

        private readonly ILiteCollection<SurveyResponse> _responses;

        public ResponseRepository(ILiteDatabase db)
        {
            LiteDbMappings.EnsureBuilt();
            _responses = db.GetCollection<SurveyResponse>(CollectionName);

            _responses.EnsureIndex(r => r.SurveyId);
        }

        public SurveyResponse Insert(SurveyResponse response)
        {
            _responses.Insert(response);
            return response;
        }

        public IEnumerable<SurveyResponse> FindBySurveyId(Guid surveyId)
        {
            return _responses.Find(r => r.SurveyId == surveyId).OrderBy(r => r.SubmittedAt);
        }

        public int CountBySurveyId(Guid surveyId)
        {
            return _responses.Count(r => r.SurveyId == surveyId);
        }

        public int DeleteBySurveyId(Guid surveyId)
        {
            return _responses.DeleteMany(r => r.SurveyId == surveyId);
        }
    }
}
