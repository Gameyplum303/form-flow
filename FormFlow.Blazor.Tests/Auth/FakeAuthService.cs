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

    public List<string> ResetRequests { get; } = [];
    public List<string> VerificationRequests { get; } = [];
    public string? NextEmailError { get; set; }

    public Task<string?> RequestPasswordResetAsync(string email)
    {
        ResetRequests.Add(email);
        return Task.FromResult(NextEmailError);
    }

    public Task<string?> ResendVerificationAsync(string email)
    {
        VerificationRequests.Add(email);
        return Task.FromResult(NextEmailError);
    }

    public (string? Status, string? Error) VerifyResult { get; set; } = (AccountStatuses.Pending, null);
    public List<string> VerifiedTokens { get; } = [];

    public Task<(string? Status, string? Error)> VerifyEmailAsync(string token)
    {
        VerifiedTokens.Add(token);
        return Task.FromResult(VerifyResult);
    }

    public FormResult ResetResult { get; set; } = FormResult.Success();
    public (string Token, string Password)? LastReset { get; private set; }

    public Task<FormResult> ResetPasswordAsync(string token, string password)
    {
        LastReset = (token, password);
        return Task.FromResult(ResetResult);
    }

    public FormResult ChangeResult { get; set; } = FormResult.Success(FakeSessionStorage.Login("admin"));
    public (string Current, string New)? LastChange { get; private set; }

    public Task<FormResult> ChangePasswordAsync(string currentPassword, string newPassword)
    {
        LastChange = (currentPassword, newPassword);
        return Task.FromResult(ChangeResult);
    }
}
