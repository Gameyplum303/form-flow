using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;

namespace FormFlow.Blazor.Tests.Auth;

public class AdminSessionTests
{
    private readonly FakeSessionStorage _storage = new();
    private readonly EphemeralDataProtectionProvider _protection = new();

    [Fact]
    public async Task SignIn_is_restored_by_a_new_circuit_in_the_same_tab()
    {
        await _storage.CreateSession(_protection).SignInAsync(FakeSessionStorage.Login("ada"));

        var reloaded = _storage.CreateSession(_protection);
        await reloaded.RestoreAsync();

        reloaded.IsRestored.Should().BeTrue();
        reloaded.IsSignedIn.Should().BeTrue();
        reloaded.Username.Should().Be("ada");
        _storage.Items.Values.Single().Should().NotContain("test-token", "the token is encrypted before it reaches the browser");
    }

    [Theory]
    [InlineData("admin", true)]
    [InlineData("viewer", false)]
    public async Task Only_the_admin_role_can_edit(string role, bool isAdmin)
    {
        var session = _storage.CreateSession(_protection);
        await session.SignInAsync(FakeSessionStorage.Login(role: role));

        var reloaded = _storage.CreateSession(_protection);
        await reloaded.RestoreAsync();

        reloaded.Role.Should().Be(role);
        reloaded.IsAdmin.Should().Be(isAdmin);
    }

    [Fact]
    public async Task Expired_sign_in_is_not_restored()
    {
        await _storage.CreateSession(_protection).SignInAsync(FakeSessionStorage.Login(lifetime: TimeSpan.FromMinutes(-1)));

        var reloaded = _storage.CreateSession(_protection);
        await reloaded.RestoreAsync();

        reloaded.IsSignedIn.Should().BeFalse();
    }

    [Fact]
    public async Task Sign_in_stored_under_other_keys_is_discarded()
    {
        await _storage.CreateSession(_protection).SignInAsync(FakeSessionStorage.Login());

        var otherServer = _storage.CreateSession(new EphemeralDataProtectionProvider());
        await otherServer.RestoreAsync();

        otherServer.IsSignedIn.Should().BeFalse();
        otherServer.IsRestored.Should().BeTrue();
        _storage.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task SignOut_clears_the_session_and_storage()
    {
        var session = _storage.CreateSession(_protection);
        await session.SignInAsync(FakeSessionStorage.Login());
        var changes = 0;
        session.Changed += () => changes++;

        await session.SignOutAsync();

        session.IsSignedIn.Should().BeFalse();
        session.Username.Should().BeNull();
        _storage.Items.Should().BeEmpty();
        changes.Should().Be(1);
    }

    [Fact]
    public async Task Authorize_sets_and_clears_the_bearer_header()
    {
        var session = _storage.CreateSession(_protection);
        using var client = new HttpClient();

        await session.SignInAsync(FakeSessionStorage.Login());
        session.Authorize(client);
        client.DefaultRequestHeaders.Authorization!.ToString().Should().Be("Bearer test-token");

        await session.SignOutAsync();
        session.Authorize(client);
        client.DefaultRequestHeaders.Authorization.Should().BeNull();
    }
}
