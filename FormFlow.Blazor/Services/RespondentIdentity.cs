using System.Security.Cryptography;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

namespace FormFlow.Blazor.Services
{
    /// <summary>
    /// The temporary identity of someone taking surveys without an account: a random id kept in this
    /// browser, so the server can refuse a second answer to the same survey. Clearing the browser's
    /// storage starts a new identity, so it stops accidental repeats rather than determined ones.
    /// </summary>
    public interface IRespondentIdentity
    {
        /// <summary>The browser's respondent id, created on first use. Only works once the page is interactive.</summary>
        Task<string> GetIdAsync();
    }

    public class RespondentIdentity(ProtectedLocalStorage storage) : IRespondentIdentity
    {
        private const string StorageKey = "formflow.respondent";
        private string? _id;

        public async Task<string> GetIdAsync()
        {
            if (_id is not null)
            {
                return _id;
            }

            try
            {
                var stored = await storage.GetAsync<string>(StorageKey);
                if (stored.Success && !string.IsNullOrWhiteSpace(stored.Value))
                {
                    return _id = stored.Value;
                }
            }
            catch (CryptographicException)
            {
                // Stored by a server whose data protection keys are gone; start a new identity.
            }

            _id = Guid.NewGuid().ToString("N");
            await storage.SetAsync(StorageKey, _id);
            return _id;
        }
    }
}
