using FormFlow.Blazor.Services;

namespace FormFlow.Blazor.Tests.Respond;

/// <summary>Saved answers kept in memory, in place of the browser's storage.</summary>
public sealed class FakeSurveyDrafts : ISurveyDrafts
{
    public Dictionary<Guid, Dictionary<string, List<string>>> Saved { get; } = new();

    public Task<Dictionary<string, List<string>>?> LoadAsync(Guid surveyId) =>
        Task.FromResult(Saved.TryGetValue(surveyId, out var answers)
            ? answers.ToDictionary(a => a.Key, a => a.Value.ToList())
            : null);

    public Task SaveAsync(Guid surveyId, Dictionary<string, List<string>> answers)
    {
        Saved[surveyId] = answers.ToDictionary(a => a.Key, a => a.Value.ToList());
        return Task.CompletedTask;
    }

    public Task ClearAsync(Guid surveyId)
    {
        Saved.Remove(surveyId);
        return Task.CompletedTask;
    }
}
