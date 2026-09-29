using FluentAssertions;
using FormFlow.Backend.Auth;
using FormFlow.Backend.Repositories;
using LiteDB;

namespace FormFlow.Backend.Tests.Repositories
{
    public class AccountTokenRepositoryTests : IDisposable
    {
        private readonly LiteDatabase _db = new(new MemoryStream());
        private readonly AccountTokenRepository _tokens;
        private readonly DateTime _now = new(2026, 9, 29, 12, 0, 0, DateTimeKind.Utc);

        public AccountTokenRepositoryTests() => _tokens = new AccountTokenRepository(_db);

        public void Dispose() => _db.Dispose();

        [Fact]
        public void StoresOnlyAHashOfTheToken()
        {
            var user = Guid.NewGuid();
            var token = _tokens.Create(user, AccountTokenPurposes.ResetPassword, _now.AddHours(1));

            var stored = _db.GetCollection<AccountToken>(AccountTokenRepository.CollectionName).FindAll().Single();
            stored.TokenHash.Should().NotContain(token).And.HaveLength(64);
            token.Length.Should().BeGreaterThanOrEqualTo(43, "32 random bytes");
        }

        [Fact]
        public void Redeem_ReturnsTheAccountOnce()
        {
            var user = Guid.NewGuid();
            var token = _tokens.Create(user, AccountTokenPurposes.VerifyEmail, _now.AddHours(1));

            _tokens.Redeem(token, AccountTokenPurposes.VerifyEmail, _now).Should().Be(user);
            _tokens.Redeem(token, AccountTokenPurposes.VerifyEmail, _now).Should().BeNull();
        }

        [Fact]
        public void Redeem_RefusesExpiredTokens_AndTokensForSomethingElse()
        {
            var user = Guid.NewGuid();
            var expired = _tokens.Create(user, AccountTokenPurposes.ResetPassword, _now);
            var verify = _tokens.Create(user, AccountTokenPurposes.VerifyEmail, _now.AddHours(1));

            _tokens.Redeem(expired, AccountTokenPurposes.ResetPassword, _now).Should().BeNull();
            _tokens.Redeem(verify, AccountTokenPurposes.ResetPassword, _now).Should().BeNull();
            _tokens.Redeem(verify, AccountTokenPurposes.VerifyEmail, _now).Should().BeNull("a wrong try uses the token up");
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("not-a-token")]
        public void Redeem_IgnoresUnknownTokens(string token) =>
            _tokens.Redeem(token, AccountTokenPurposes.VerifyEmail, _now).Should().BeNull();

        [Fact]
        public void NewTokens_ReplaceOlderOnes_ForTheSamePurposeOnly()
        {
            var user = Guid.NewGuid();
            var firstReset = _tokens.Create(user, AccountTokenPurposes.ResetPassword, _now.AddHours(1));
            var verify = _tokens.Create(user, AccountTokenPurposes.VerifyEmail, _now.AddHours(1));
            var secondReset = _tokens.Create(user, AccountTokenPurposes.ResetPassword, _now.AddHours(1));

            _tokens.Redeem(firstReset, AccountTokenPurposes.ResetPassword, _now).Should().BeNull();
            _tokens.Redeem(secondReset, AccountTokenPurposes.ResetPassword, _now).Should().Be(user);
            _tokens.Redeem(verify, AccountTokenPurposes.VerifyEmail, _now).Should().Be(user);
        }

        [Fact]
        public void SessionCheck_EndsTokensIssuedBeforeThePasswordChanged()
        {
            var user = new AdminUser { PasswordChangedAt = new DateTime(2026, 9, 29, 12, 0, 5, 700, DateTimeKind.Utc) };

            SessionCheck.IsCurrent(user, new DateTime(2026, 9, 29, 12, 0, 4, DateTimeKind.Utc)).Should().BeFalse();
            SessionCheck.IsCurrent(user, new DateTime(2026, 9, 29, 12, 0, 5, DateTimeKind.Utc)).Should().BeTrue(
                "the token handed out with the change is issued in the same second");
            SessionCheck.IsCurrent(new AdminUser(), DateTime.MinValue).Should().BeTrue("the password never changed");
            SessionCheck.IsCurrent(null, DateTime.UtcNow).Should().BeFalse("the account is gone");
        }
    }
}
