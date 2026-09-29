using System.Security.Claims;
using FormFlow.Data.Models;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace FormFlow.Backend.Auth
{
    public class TokenService(JwtSettings settings, TimeProvider clock)
    {
        public LoginResponse CreateToken(AdminUser user)
        {
            var now = clock.GetUtcNow().UtcDateTime;
            var expires = now.Add(settings.Lifetime);
            var token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
            {
                Issuer = settings.Issuer,
                Audience = settings.Audience,
                IssuedAt = now,
                NotBefore = now,
                Expires = expires,
                Subject = new ClaimsIdentity(
                [
                    new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                    new Claim(JwtRegisteredClaimNames.UniqueName, user.Username),
                    new Claim("role", JwtSettings.AdminRole),
                ]),
                SigningCredentials = new SigningCredentials(settings.SigningKey, SecurityAlgorithms.HmacSha256),
            });

            return new LoginResponse { Token = token, Username = user.Username, ExpiresAt = expires };
        }
    }
}
