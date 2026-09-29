using System.Diagnostics.CodeAnalysis;
using FormFlow.Blazor.Services;
using FormFlow.Data.Models;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.JSInterop;

namespace FormFlow.Blazor.Tests.Auth;

/// <summary>Stands in for the browser's sessionStorage, which ProtectedSessionStorage reaches through JS interop.</summary>
public sealed class FakeSessionStorage : IJSRuntime
{
    public Dictionary<string, string> Items { get; } = new();

    public ValueTask<TValue> InvokeAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.PublicProperties)] TValue>(
        string identifier, object?[]? args) =>
        InvokeAsync<TValue>(identifier, CancellationToken.None, args);

    public ValueTask<TValue> InvokeAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.PublicProperties)] TValue>(
        string identifier, CancellationToken cancellationToken, object?[]? args)
    {
        var key = (string)args![0]!;
        switch (identifier)
        {
            case "sessionStorage.setItem":
                Items[key] = (string)args[1]!;
                return ValueTask.FromResult(default(TValue)!);
            case "sessionStorage.getItem":
                return ValueTask.FromResult(Items.TryGetValue(key, out var value) ? (TValue)(object)value : default!);
            case "sessionStorage.removeItem":
                Items.Remove(key);
                return ValueTask.FromResult(default(TValue)!);
            default:
                throw new InvalidOperationException($"Unexpected JS call {identifier}.");
        }
    }

    /// <summary>A session backed by this storage. Pass the same provider to read what another session stored.</summary>
    public AdminSession CreateSession(IDataProtectionProvider? protection = null) =>
        new(new ProtectedSessionStorage(this, protection ?? new EphemeralDataProtectionProvider()));

    public static LoginResponse Login(string username = "admin", TimeSpan? lifetime = null, string role = "admin") => new()
    {
        Token = "test-token",
        Username = username,
        Role = role,
        ExpiresAt = DateTime.UtcNow + (lifetime ?? TimeSpan.FromHours(1))
    };
}
