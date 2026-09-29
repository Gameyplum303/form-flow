using System.Linq.Expressions;

using Question = FormFlow.Data.Models.QuestionDefinition;

namespace FormFlow.Backend.Repositories
{
    public interface IQuestionRepository
    {
        Question Insert(Question question);
        Question? FindById(Guid id);
        IEnumerable<Question> FindAll();

        Question? FindOne(Expression<Func<Question, bool>> predicate);

        /// <summary>Replaces the stored question. Returns false when it does not exist.</summary>
        bool Update(Question question);

        /// <summary>Deletes the question. Returns false when it does not exist.</summary>
        bool Delete(Guid id);
    }
}
