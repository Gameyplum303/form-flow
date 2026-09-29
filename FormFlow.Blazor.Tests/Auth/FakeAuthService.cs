using FormFlow.Blazor.Services;
using FormFlow.Data.Models;

namespace FormFlow.Blazor.Tests.Auth;

public sealed class FakeAuthService : IAuthService
{
    public (LoginResponse? Login, string? Error) Result { get; set; } = (FakeSessionStorage.Login(), null);
    public (string Username, string Password)? LastAttempt { get; private set; }

    public SignUpResult SignUpResult { get; set; } = new(AccountStatuses.Pending, new Dictionary<string, string[]>(), null);
    public List<SignUpRequest> SignUps { get; } = [];

    public Task<(LoginResponse? Login, string? Error)> LoginAsync(string username, string password)
    {
        LastAttempt = (username, password);
        return Task.FromResult(Result);
    }

    public Task<SignUpResult> SignUpAsync(SignUpRequest request)
    {
        SignUps.Add(request);
        return Task.FromResult(SignUpResult);
    }
}
