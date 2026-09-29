using FormFlow.Backend.Auth;

namespace FormFlow.Backend.Repositories
{
    public interface IAccountTokenRepository
    {
        /// <summary>
        /// Creates a token for the account and returns the text to put in the link. Earlier tokens for the
        /// same account and purpose stop working, so only the newest link is valid.
        /// </summary>
        string Create(Guid userId, string purpose, DateTime expiresAt);

        /// <summary>
        /// Uses up a token: returns the account it belongs to and deletes it, or null when the token is
        /// unknown, expired or meant for something else.
        /// </summary>
        Guid? Redeem(string token, string purpose, DateTime utcNow);

        /// <summary>Removes the account's tokens, for example when it is deleted.</summary>
        void DeleteForUser(Guid userId);
    }
}
