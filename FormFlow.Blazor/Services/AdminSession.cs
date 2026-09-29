using System.Net.Http.Headers;
using System.Security.Cryptography;
using FormFlow.Data.Models;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

namespace FormFlow.Blazor.Services
{
    /// <summary>
    /// The signed-in account for one browser tab. The token is kept in encrypted session storage,
    /// so a page reload keeps the account signed in and closing the tab signs it out.
    /// </summary>
    public class AdminSession(ProtectedSessionStorage storage)
    {
        private const string StorageKey = "formflow.admin";
        private const string ViewAsKey = "formflow.viewas";

        public string? Token { get; private set; }
        public Guid? UserId { get; private set; }
        public string? Username { get; private set; }
        public string? Role { get; private set; }
        public DateTime ExpiresAt { get; private set; }

        /// <summary>
        /// The role an administrator is previewing the site as, or null for their own view. The server
        /// still treats them as an administrator; only what the pages show changes.
        /// </summary>
        public string? ViewAs { get; private set; }

        /// <summary>Whether the stored sign-in has been read. Storage is only readable once the page is interactive.</summary>
        public bool IsRestored { get; private set; }

        public bool IsSignedIn => Token is not null && ExpiresAt > DateTime.UtcNow;

        /// <summary>Whether the account really is an administrator, whatever view it is previewing.</summary>
        public bool IsAdmin => IsSignedIn && Role == Roles.Admin;

        /// <summary>The role the pages are shown for: the preview role for an administrator who picked one.</summary>
        public string? EffectiveRole => !IsSignedIn ? null : IsAdmin && ViewAs is not null ? ViewAs : Role;

        public bool IsPreviewing => IsAdmin && ViewAs is not null;

        /// <summary>What the admin pages may show and do for the current view.</summary>
        public AdminAccess Access => new(EffectiveRole ?? string.Empty, UserId);

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
                    var viewAs = await storage.GetAsync<string>(ViewAsKey);
                    ViewAs = IsAdmin && viewAs.Success && Roles.IsKnown(viewAs.Value) && viewAs.Value != Roles.Admin
                        ? viewAs.Value
                        : null;
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
            UserId = null;
            Username = null;
            Role = null;
            ViewAs = null;
            await storage.DeleteAsync(StorageKey);
            await storage.DeleteAsync(ViewAsKey);
            Changed?.Invoke();
        }

        /// <summary>Lets an administrator see the site as a professor or a student, or go back to their own view.</summary>
        public async Task SetViewAsAsync(string? role)
        {
            if (!IsAdmin)
            {
                return;
            }

            ViewAs = role is Roles.Professor or Roles.Student ? role : null;
            if (ViewAs is null)
            {
                await storage.DeleteAsync(ViewAsKey);
            }
            else
            {
                await storage.SetAsync(ViewAsKey, ViewAs);
            }
            Changed?.Invoke();
        }

        /// <summary>Adds the bearer token to requests from this client, or removes it when signed out.</summary>
        public void Authorize(HttpClient client) =>
            client.DefaultRequestHeaders.Authorization = IsSignedIn ? new AuthenticationHeaderValue("Bearer", Token) : null;

        private void Apply(LoginResponse login)
        {
            Token = login.Token;
            UserId = login.UserId == Guid.Empty ? null : login.UserId;
            Username = login.Username;
            Role = login.Role;
            ExpiresAt = login.ExpiresAt;
        }
    }
}
