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

            login.Username.Should().Be("student");
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
            (await AdminClient.LoginAsync(_client, "STUDENT", "password")).Username.Should().Be("student");
        }

        [Fact]
        public void Seeder_AddsNewAccounts_AndLeavesExistingPasswordsAlone()
        {
            var users = _factory.Services.GetRequiredService<IUserRepository>();
            var hasher = _factory.Services.GetRequiredService<IPasswordHasher<AdminUser>>();
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AdminAccounts:0:Username"] = "student",
                ["AdminAccounts:0:Password"] = "changed",
                ["AdminAccounts:1:Username"] = " Grace ",
                ["AdminAccounts:1:Password"] = "hopper",
                ["AdminAccounts:2:Username"] = "no-password",
            }).Build();

            new AdminAccountSeeder(users, hasher, config, NullLogger<AdminAccountSeeder>.Instance).Seed();

            var student = users.FindByUsername("student")!;
            hasher.VerifyHashedPassword(student, student.PasswordHash, "password").Should().Be(PasswordVerificationResult.Success);
            var grace = users.FindByUsername("grace")!;
            grace.Username.Should().Be("Grace");
            hasher.VerifyHashedPassword(grace, grace.PasswordHash, "hopper").Should().Be(PasswordVerificationResult.Success);
            users.FindByUsername("no-password").Should().BeNull();
            users.Count().Should().Be(3);
        }

        [Theory]
        [InlineData("student", "wrong-password")]
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
            var admin = users.FindByUsername("student")!;

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
            me.GetProperty("username").GetString().Should().Be("student");
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
        public async Task AdminEndpoints_RejectAnonymousCalls(string method, string path)
        {
            var survey = await InMemoryApiFactory.GetDemoSurveyAsync(_client);
            var question = await InMemoryApiFactory.GetQuestionAsync(_client, "first_name");
            var request = new HttpRequestMessage(new HttpMethod(method),
                path.Replace("{survey}", survey.Id.ToString()).Replace("{question}", question.Id.ToString()))
            {
                Content = method is "POST" or "PUT" ? new StringContent("{}", Encoding.UTF8, "application/json") : null
            };

            var response = await _client.SendAsync(request);

            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
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
                var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Username = "student", Password = "wrong" });
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
