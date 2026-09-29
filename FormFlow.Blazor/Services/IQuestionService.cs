
using FormFlow.Data.Models;

namespace FormFlow.Blazor.Services
{
    public interface IQuestionService
    {
        /// <summary>Every question in the bank, or null when they could not be loaded.</summary>
        Task<List<QuestionDefinition>?> GetAllQuestionsAsync();

        /// <summary>The question, or null when it doesn't exist or the server can't be reached.</summary>
        Task<QuestionDefinition?> GetQuestionAsync(Guid id);
        Task<(bool Success, string? Error)> CreateQuestionAsync(NewQuestion newQuestion);
        Task<(bool Success, string? Error)> UpdateQuestionAsync(Guid id, NewQuestion question);
        Task<(bool Success, string? Error)> DeleteQuestionAsync(Guid id);
    }
}
