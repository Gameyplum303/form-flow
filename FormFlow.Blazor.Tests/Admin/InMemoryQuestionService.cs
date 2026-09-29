using FormFlow.Blazor.Services;
using FormFlow.Data.Models;

namespace FormFlow.Blazor.Tests.Admin;

/// <summary>An IQuestionService over a list, for page tests that don't need HTTP.</summary>
public sealed class InMemoryQuestionService : IQuestionService
{
    public List<QuestionDefinition> Questions { get; } = new();
    public (bool Success, string? Error) NextDeleteResult { get; set; } = (true, null);
    public List<Guid> Deleted { get; } = new();

    public Task<List<QuestionDefinition>?> GetAllQuestionsAsync() =>
        Task.FromResult<List<QuestionDefinition>?>(Questions.ToList());

    public Task<QuestionDefinition?> GetQuestionAsync(Guid id) =>
        Task.FromResult(Questions.FirstOrDefault(q => q.Id == id));

    public Task<(bool Success, string? Error)> CreateQuestionAsync(NewQuestion newQuestion) =>
        Task.FromResult((true, (string?)null));

    public Task<(bool Success, string? Error)> UpdateQuestionAsync(Guid id, NewQuestion question) =>
        Task.FromResult((true, (string?)null));

    public Task<(bool Success, string? Error)> DeleteQuestionAsync(Guid id)
    {
        if (NextDeleteResult.Success)
        {
            Deleted.Add(id);
        }
        return Task.FromResult(NextDeleteResult);
    }
}
