using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using FormFlow.Backend.Auth;
using FormFlow.Backend.Repositories;
using FormFlow.Data.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Tokens;

namespace FormFlow.Backend.Tests.Endpoints
{
    public class AuthEndpointTests : IDisposable
    {
        private readonly InMemoryApiFactory _factory = new();
        private readonly HttpClient _client;

        public AuthEndpointTests()
        {
            _client = _factory.CreateClient();
        }

        public void Dispose() => _factory.Dispose();

        [Fact]
        public async Task Login_WithCorrectPassword_ReturnsAToken()
        {
            var login = await AdminClient.LoginAsync(_client);

            login.Username.Should().Be("Rogers");
            login.Role.Should().Be("admin");
            login.Token.Should().NotBeNullOrWhiteSpace();
            login.ExpiresAt.Should().BeAfter(DateTime.UtcNow.AddHours(7));
        }

        [Fact]
        public async Task Login_UsernameIsCaseInsensitive_AndKeepsItsOriginalCase()
        {
            var login = await AdminClient.LoginAsync(_client, "rogers", "password");

            login.Username.Should().Be("Rogers");
        }

        [Fact]
        public async Task EveryConfiguredAccount_CanSignIn()
        {
            (await AdminClient.LoginAsync(_client, "Rogers", "password")).Token.Should().NotBeNullOrWhiteSpace();
            var professor = await AdminClient.LoginAsync(_client, "PROFESSOR", "password");
            professor.Username.Should().Be("professor");
            professor.Role.Should().Be("professor");
            professor.UserId.Should().NotBeEmpty();
        }

        [Fact]
        public void Seeder_AddsNewAccounts_UpdatesRoles_AndLeavesExistingPasswordsAlone()
        {
            var users = _factory.Services.GetRequiredService<IUserRepository>();
            var hasher = _factory.Services.GetRequiredService<IPasswordHasher<AdminUser>>();
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Accounts:0:Username"] = "professor",
                ["Accounts:0:Password"] = "changed",
                ["Accounts:0:Role"] = "admin",
                ["Accounts:1:Username"] = " Grace ",
                ["Accounts:1:Password"] = "hopper",
                ["Accounts:2:Username"] = "no-password",
                ["Accounts:3:Username"] = "odd-role",
                ["Accounts:3:Password"] = "x",
                ["Accounts:3:Role"] = "superuser",
                ["Accounts:4:Username"] = "a-student",
                ["Accounts:4:Password"] = "x",
                ["Accounts:4:Role"] = "student",
            }).Build();

            new AdminAccountSeeder(users, hasher, config, NullLogger<AdminAccountSeeder>.Instance, TimeProvider.System).Seed();

            var professor = users.FindByUsername("professor")!;
            hasher.VerifyHashedPassword(professor, professor.PasswordHash, "password").Should().Be(PasswordVerificationResult.Success);
            professor.Role.Should().Be("admin");
            var grace = users.FindByUsername("grace")!;
            grace.Username.Should().Be("Grace");
            grace.Role.Should().Be("professor", "administrators have to be listed as admins");
            grace.Status.Should().Be("active");
            hasher.VerifyHashedPassword(grace, grace.PasswordHash, "hopper").Should().Be(PasswordVerificationResult.Success);
            users.FindByUsername("no-password").Should().BeNull();
            users.FindByUsername("odd-role").Should().BeNull();
            users.FindByUsername("a-student").Should().BeNull("students take surveys without an account");
            users.Count().Should().Be(3);
        }

        [Theory]
        [InlineData("Rogers", "wrong-password")]
        [InlineData("nobody", "password")]
        [InlineData("", "")]
        public async Task Login_WithBadCredentials_Returns401WithoutSayingWhichPartWasWrong(string username, string password)
        {
            var response = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Username = username, Password = password });

            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            (await response.Content.ReadAsStringAsync()).Should().Contain("Invalid username or password.");
        }

        [Fact]
        public void Password_IsStoredHashed()
        {
            var users = _factory.Services.GetRequiredService<IUserRepository>();
            var admin = users.FindByUsername("Rogers")!;

            admin.PasswordHash.Should().NotContain("password");
            _factory.Services.GetRequiredService<IPasswordHasher<AdminUser>>()
                .VerifyHashedPassword(admin, admin.PasswordHash, "password")
                .Should().Be(PasswordVerificationResult.Success);
        }

        [Fact]
        public async Task Me_RequiresAToken()
        {
            (await _client.GetAsync("/api/auth/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

            _client.AsAdmin();
            var me = await _client.GetFromJsonAsync<JsonElement>("/api/auth/me");
            me.GetProperty("username").GetString().Should().Be("Rogers");
            me.GetProperty("role").GetString().Should().Be("admin");
            me.GetProperty("id").GetGuid().Should().NotBeEmpty();
        }

        [Theory]
        [InlineData("POST", "/api/questions")]
        [InlineData("PUT", "/api/questions/{question}")]
        [InlineData("DELETE", "/api/questions/{question}")]
        [InlineData("POST", "/api/surveys")]
        [InlineData("PUT", "/api/surveys/{survey}")]
        [InlineData("DELETE", "/api/surveys/{survey}")]
        [InlineData("GET", "/api/surveys/{survey}/responses")]
        [InlineData("GET", "/api/surveys/{survey}/results")]
        [InlineData("GET", "/api/surveys/{survey}/responses/export")]
        [InlineData("GET", "/api/surveys/managed")]
        [InlineData("PUT", "/api/surveys/{survey}/sharing")]
        [InlineData("POST", "/api/auth/change-password")]
        public async Task AdminEndpoints_RejectAnonymousCalls(string method, string path)
        {
            var response = await _client.SendAsync(await RequestAsync(method, path));

            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Theory]
        [InlineData("GET", "/api/accounts/pending")]
        [InlineData("POST", "/api/accounts/{account}/approve")]
        [InlineData("POST", "/api/accounts/{account}/decline")]
        [InlineData("GET", "/api/accounts/outbox")]
        public async Task OnlyAdministrators_ReviewSignUps(string method, string path)
        {
            var request = await RequestAsync(method, path);
            (await _client.SendAsync(await RequestAsync(method, path))).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

            _client.AsProfessor();

            (await _client.SendAsync(request)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        private async Task<HttpRequestMessage> RequestAsync(string method, string path)
        {
            var survey = await InMemoryApiFactory.GetDemoSurveyAsync(_client);
            var question = await InMemoryApiFactory.GetQuestionAsync(_client, "first_name");
            return new HttpRequestMessage(new HttpMethod(method),
                path.Replace("{survey}", survey.Id.ToString()).Replace("{question}", question.Id.ToString())
                    .Replace("{account}", Guid.NewGuid().ToString()))
            {
                Content = method is "POST" or "PUT" ? new StringContent("{}", Encoding.UTF8, "application/json") : null
            };
        }

        [Fact]
        public async Task TakingASurvey_StaysAnonymous()
        {
            var survey = await InMemoryApiFactory.GetDemoSurveyAsync(_client);

            (await _client.GetAsync("/api/surveys")).StatusCode.Should().Be(HttpStatusCode.OK);
            (await _client.GetAsync($"/api/surveys/{survey.Id}/questions")).StatusCode.Should().Be(HttpStatusCode.OK);
            var submit = await _client.PostAsJsonAsync($"/api/surveys/{survey.Id}/responses", ResponseEndpointTests.ValidAnswers());
            submit.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        [Fact]
        public async Task TokenSignedWithAnotherKey_IsRejected()
        {
            var forged = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
                issuer: "FormFlow",
                audience: "FormFlow",
                claims: [new("role", "admin"), new("unique_name", "admin")],
                expires: DateTime.UtcNow.AddHours(1),
                signingCredentials: new SigningCredentials(
                    new SymmetricSecurityKey(Encoding.UTF8.GetBytes("not-the-real-key-but-long-enough-to-sign")),
                    SecurityAlgorithms.HmacSha256)));
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", forged);

            (await _client.GetAsync("/api/auth/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task Login_IsRateLimited()
        {
            using var factory = new InMemoryApiFactory();
            var client = factory.WithWebHostBuilder(b => b.UseSetting("RateLimits:LoginPerMinute", "3")).CreateClient();

            var statuses = new List<HttpStatusCode>();
            for (var i = 0; i < 4; i++)
            {
                var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Username = "Rogers", Password = "wrong" });
                statuses.Add(response.StatusCode);
            }

            statuses.Should().Equal(HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized,
                HttpStatusCode.TooManyRequests);
        }

        [Fact]
        public async Task OpenApi_DescribesTheBearerTokenOnAdminEndpoints()
        {
            var doc = await _client.GetFromJsonAsync<JsonElement>("/openapi/v1.json");

            doc.GetProperty("components").GetProperty("securitySchemes").GetProperty("Bearer")
                .GetProperty("scheme").GetString().Should().Be("bearer");
            doc.GetProperty("paths").GetProperty("/api/questions").GetProperty("post")
                .TryGetProperty("security", out _).Should().BeTrue();
            doc.GetProperty("paths").GetProperty("/api/questions").GetProperty("get")
                .TryGetProperty("security", out _).Should().BeFalse();
        }
    }
}
