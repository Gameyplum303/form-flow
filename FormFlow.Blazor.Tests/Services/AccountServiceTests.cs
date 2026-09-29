using System.Net;
using System.Text;
using FluentAssertions;
using FormFlow.Blazor.Services;
using FormFlow.Blazor.Tests.Auth;
using RichardSzalay.MockHttp;

namespace FormFlow.Blazor.Tests.Services;

public class AccountServiceTests
{
    private readonly MockHttpMessageHandler _http = new();
    private readonly FakeSessionStorage _storage = new();

    private async Task<AccountService> CreateAsync()
    {
        var session = _storage.CreateSession();
        await session.SignInAsync(FakeSessionStorage.Login());
        return new AccountService(new HttpClient(_http) { BaseAddress = new Uri("http://api.test/") }, session);
    }

    [Fact]
    public async Task GetPendingAsync_sends_the_token_and_reads_the_list()
    {
        _http.When(HttpMethod.Get, "http://api.test/api/accounts/pending")
            .WithHeaders("Authorization", $"Bearer {FakeSessionStorage.Login().Token}")
            .Respond(new StringContent("""[{"id":"7d1f3c1e-8a52-4a4f-9d1c-0b6d4e0f2a11","name":"Ada","email":"ada@lab.example"}]""",
                Encoding.UTF8, "application/json"));
        var service = await CreateAsync();

        var pending = await service.GetPendingAsync();

        pending.Should().ContainSingle().Which.Email.Should().Be("ada@lab.example");
    }

    [Theory]
    [InlineData(HttpStatusCode.NoContent, null)]
    [InlineData(HttpStatusCode.NotFound, "That sign-up is no longer waiting. Another administrator may have handled it.")]
    [InlineData(HttpStatusCode.Forbidden, "Only administrators can review sign-ups.")]
    [InlineData(HttpStatusCode.InternalServerError, "Request failed (500). Please try again.")]
    public async Task Approve_and_decline_explain_failures(HttpStatusCode status, string? expected)
    {
        var id = Guid.NewGuid();
        _http.When(HttpMethod.Post, $"http://api.test/api/accounts/{id}/approve").Respond(status);
        _http.When(HttpMethod.Post, $"http://api.test/api/accounts/{id}/decline").Respond(status);
        var service = await CreateAsync();

        (await service.ApproveAsync(id)).Should().Be(expected);
        (await service.DeclineAsync(id)).Should().Be(expected);
    }
}
