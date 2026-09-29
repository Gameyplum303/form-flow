using System.Security.Cryptography;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.JSInterop;

namespace FormFlow.Blazor.Services
{
    /// <summary>
    /// Answers someone has given but not yet submitted, kept in this browser per survey, so closing the
    /// tab or coming back later picks up where they left off. Browser storage is only reachable once the
    /// page is interactive, so pages call this after their first render.
    /// </summary>
    public interface ISurveyDrafts
    {
        /// <summary>The saved answers for the survey, or null when there are none (or they can't be read).</summary>
        Task<Dictionary<string, List<string>>?> LoadAsync(Guid surveyId);

        Task SaveAsync(Guid surveyId, Dictionary<string, List<string>> answers);

        Task ClearAsync(Guid surveyId);
    }

    public class SurveyDrafts(ProtectedLocalStorage storage) : ISurveyDrafts
    {
        private static string KeyFor(Guid surveyId) => $"formflow.answers.{surveyId:N}";

        public async Task<Dictionary<string, List<string>>?> LoadAsync(Guid surveyId)
        {
            try
            {
                var stored = await storage.GetAsync<Dictionary<string, List<string>>>(KeyFor(surveyId));
                return stored.Success ? stored.Value : null;
            }
            catch (CryptographicException)
            {
                // Saved by a server whose data protection keys are gone; start again.
                return null;
            }
            catch (JSException)
            {
                // Storage is blocked, or the value isn't what we saved.
                return null;
            }
        }

        public async Task SaveAsync(Guid surveyId, Dictionary<string, List<string>> answers)
        {
            try
            {
                await storage.SetAsync(KeyFor(surveyId), answers);
            }
            catch (JSException)
            {
                // Storage is full or blocked; answering still works, it just won't be remembered.
            }
        }

        public async Task ClearAsync(Guid surveyId)
        {
            try
            {
                await storage.DeleteAsync(KeyFor(surveyId));
            }
            catch (JSException)
            {
                // Nothing we can do; the answers were submitted anyway.
            }
        }
    }
}
