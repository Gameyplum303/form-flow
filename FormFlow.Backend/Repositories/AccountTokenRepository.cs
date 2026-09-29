using System.Security.Cryptography;
using System.Text;
using FormFlow.Backend.Auth;
using LiteDB;
using Microsoft.AspNetCore.WebUtilities;

namespace FormFlow.Backend.Repositories
{
    public class AccountTokenRepository : IAccountTokenRepository
    {
        public const string CollectionName = "account_tokens";

        private readonly ILiteCollection<AccountToken> _tokens;

        public AccountTokenRepository(ILiteDatabase db)
        {
            LiteDbMappings.EnsureBuilt();
            _tokens = db.GetCollection<AccountToken>(CollectionName);
            _tokens.EnsureIndex(t => t.TokenHash, true);
            _tokens.EnsureIndex(t => t.UserId);
        }

        public string Create(Guid userId, string purpose, DateTime expiresAt)
        {
            _tokens.DeleteMany(t => t.UserId == userId && t.Purpose == purpose);

            // 32 random bytes: far too many to guess, and short enough for a link.
            var token = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
            _tokens.Insert(new AccountToken
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Purpose = purpose,
                TokenHash = Hash(token),
                ExpiresAt = expiresAt,
            });
            return token;
        }

        public Guid? Redeem(string token, string purpose, DateTime utcNow)
        {
            if (string.IsNullOrWhiteSpace(token) || token.Length > 100)
            {
                return null;
            }

            var hash = Hash(token.Trim());
            var stored = _tokens.FindOne(t => t.TokenHash == hash);
            if (stored is null)
            {
                return null;
            }

            // Delete it whatever happens next: a token is good for one try.
            _tokens.Delete(stored.Id);
            return stored.Purpose == purpose && stored.ExpiresAt > utcNow ? stored.UserId : null;
        }

        public void DeleteForUser(Guid userId) => _tokens.DeleteMany(t => t.UserId == userId);

        private static string Hash(string token) =>
            Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }
}
