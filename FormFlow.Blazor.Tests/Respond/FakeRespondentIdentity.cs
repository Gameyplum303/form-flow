using FormFlow.Blazor.Services;

namespace FormFlow.Blazor.Tests.Respond;

/// <summary>A fixed respondent id, in place of the one kept in browser storage.</summary>
public sealed class FakeRespondentIdentity(string id = "respondent-1") : IRespondentIdentity
{
    public string Id { get; } = id;

    public Task<string> GetIdAsync() => Task.FromResult(Id);
}
