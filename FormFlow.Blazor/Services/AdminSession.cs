using System.Net.Http.Headers;
using System.Security.Cryptography;
using FormFlow.Data.Models;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

namespace FormFlow.Blazor.Services
{
    /// <summary>
    /// The signed-in admin for one browser tab. The token is kept in encrypted session storage,
    /// so a page reload keeps the admin signed in and closing the tab signs them out.
    /// </summary>
    public class AdminSession(ProtectedSessionStorage storage)
    {
        private const string StorageKey = "formflow.admin";

        public string? Token { get; private set; }
        public string? Username { get; private set; }
        public string? Role { get; private set; }
        public DateTime ExpiresAt { get; private set; }

        /// <summary>Whether the stored sign-in has been read. Storage is only readable once the page is interactive.</summary>
        public bool IsRestored { get; private set; }

        public bool IsSignedIn => Token is not null && ExpiresAt > DateTime.UtcNow;

        /// <summary>Admins can change questions and surveys. Other accounts can only look.</summary>
        public bool IsAdmin => IsSignedIn && Role == "admin";

        public event Action? Changed;

        public async Task RestoreAsync()
        {
            if (IsRestored)
            {
                return;
            }

            try
            {
                var stored = await storage.GetAsync<LoginResponse>(StorageKey);
                if (stored.Success && stored.Value is { } login && login.ExpiresAt > DateTime.UtcNow)
                {
                    Apply(login);
                }
            }
            catch (CryptographicException)
            {
                // Stored by a server whose data protection keys are gone, for example a replaced container.
                await storage.DeleteAsync(StorageKey);
            }

            IsRestored = true;
            Changed?.Invoke();
        }

        public async Task SignInAsync(LoginResponse login)
        {
            Apply(login);
            IsRestored = true;
            await storage.SetAsync(StorageKey, login);
            Changed?.Invoke();
        }

        public async Task SignOutAsync()
        {
            Token = null;
            Username = null;
            Role = null;
            await storage.DeleteAsync(StorageKey);
            Changed?.Invoke();
        }

        /// <summary>Adds the bearer token to requests from this client, or removes it when signed out.</summary>
        public void Authorize(HttpClient client) =>
            client.DefaultRequestHeaders.Authorization = IsSignedIn ? new AuthenticationHeaderValue("Bearer", Token) : null;

        private void Apply(LoginResponse login)
        {
            Token = login.Token;
            Username = login.Username;
            Role = login.Role;
            ExpiresAt = login.ExpiresAt;
        }
    }
}
