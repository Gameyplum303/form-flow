using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using FormFlow.Backend.Endpoints;
using FormFlow.Backend.Repositories;
using FormFlow.Data.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace FormFlow.Backend.Tests.Endpoints
{
    /// <summary>Professors and scientists sign up, then wait for an administrator to approve them.</summary>
    public class SignUpEndpointTests : IDisposable
    {
        private readonly InMemoryApiFactory _factory = new();
        private readonly HttpClient _client;

        public SignUpEndpointTests()
        {
            _client = _factory.CreateClient();
        }

        public void Dispose() => _factory.Dispose();

        public static SignUpRequest ValidSignUp(string email = "ada@lab.example") => new()
        {
            Name = "Ada Lovelace",
            Email = email,
            Password = "analytical",
            DateOfBirth = "1990-12-10",
            IntendedUse = "Surveys for my lab's study participants.",
            Organization = "Analytical Engines Lab",
        };

        private Task<HttpResponseMessage> SignUpAsync(SignUpRequest request) => _client.PostAsJsonAsync("/api/auth/signup", request);

        private Task<HttpResponseMessage> LoginAsync(string username, string password) =>
            _factory.CreateClient().PostAsJsonAsync("/api/auth/login", new LoginRequest { Username = username, Password = password });

        private Task VerifyAsync(string email = "ada@lab.example") =>
            InMemoryApiFactory.VerifyEmailAsync(_client, _factory.Services, email);

        [Fact]
        public async Task SignUp_StoresAPendingProfessor_WhoCantSignInYet()
        {
            var response = await SignUpAsync(ValidSignUp());

            response.StatusCode.Should().Be(HttpStatusCode.Created);
            var created = (await response.Content.ReadFromJsonAsync<SignUpResponse>())!;
            created.Status.Should().Be("pending");
            created.EmailVerificationRequired.Should().BeTrue();

            var user = _factory.Services.GetRequiredService<IUserRepository>().FindByUsername("ADA@lab.example")!;
            user.Role.Should().Be("professor");
            user.Status.Should().Be("pending");
            user.Name.Should().Be("Ada Lovelace");
            user.DateOfBirth.Should().Be("1990-12-10");
            user.Organization.Should().Be("Analytical Engines Lab");
            user.PasswordHash.Should().NotContain("analytical");
            user.EmailVerified.Should().BeFalse();

            var unverified = await LoginAsync("ada@lab.example", "analytical");
            unverified.StatusCode.Should().Be(HttpStatusCode.Forbidden);
            var problem = await unverified.Content.ReadFromJsonAsync<JsonElement>();
            problem.GetProperty("title").GetString().Should().Be("Please verify your email address first.");
            problem.GetProperty("reason").GetString().Should().Be(SignInBlocks.EmailUnverified);

            await VerifyAsync();
            var login = await LoginAsync("ada@lab.example", "analytical");
            login.StatusCode.Should().Be(HttpStatusCode.Forbidden);
            var pending = await login.Content.ReadFromJsonAsync<JsonElement>();
            pending.GetProperty("title").GetString().Should().Contain("waiting for an administrator's approval");
            pending.GetProperty("reason").GetString().Should().Be(SignInBlocks.Pending);

            (await LoginAsync("ada@lab.example", "wrong")).StatusCode.Should().Be(HttpStatusCode.Unauthorized,
                "a wrong password doesn't reveal that the account is waiting");
        }

        [Fact]
        public async Task ApprovedProfessor_CanSignIn_AndBuildSurveys()
        {
            await SignUpAsync(ValidSignUp());
            var admin = _factory.CreateClient().AsAdmin();

            var pending = await admin.GetFromJsonAsync<List<PendingAccount>>("/api/accounts/pending");
            var ada = pending!.Should().ContainSingle().Subject;
            ada.Email.Should().Be("ada@lab.example");
            ada.IntendedUse.Should().Be("Surveys for my lab's study participants.");
            ada.DateOfBirth.Should().Be("1990-12-10");
            ada.EmailVerified.Should().BeFalse();

            await VerifyAsync();
            (await admin.GetFromJsonAsync<List<PendingAccount>>("/api/accounts/pending"))!.Single().EmailVerified.Should().BeTrue();
            (await admin.PostAsync($"/api/accounts/{ada.Id}/approve", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);

            (await admin.GetFromJsonAsync<List<PendingAccount>>("/api/accounts/pending")).Should().BeEmpty();
            var login = await LoginAsync("ada@lab.example", "analytical");
            login.StatusCode.Should().Be(HttpStatusCode.OK);
            (await login.Content.ReadFromJsonAsync<LoginResponse>())!.Role.Should().Be("professor");

            var professor = _factory.CreateClient().SignedInAs("ada@lab.example", "analytical");
            (await professor.GetAsync("/api/surveys/managed")).StatusCode.Should().Be(HttpStatusCode.OK);
            (await admin.PostAsync($"/api/accounts/{ada.Id}/approve", null)).StatusCode.Should().Be(HttpStatusCode.NotFound,
                "the account is no longer waiting");
        }

        [Fact]
        public async Task DecliningASignUp_RemovesIt_SoThePersonCanTryAgain()
        {
            await SignUpAsync(ValidSignUp());
            var admin = _factory.CreateClient().AsAdmin();
            var ada = (await admin.GetFromJsonAsync<List<PendingAccount>>("/api/accounts/pending"))!.Single();

            (await admin.PostAsync($"/api/accounts/{ada.Id}/decline", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);

            (await LoginAsync("ada@lab.example", "analytical")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            (await SignUpAsync(ValidSignUp())).StatusCode.Should().Be(HttpStatusCode.Created);
        }

        [Fact]
        public async Task Declining_OnlyWorksOnSignUps_NotOnActiveAccounts()
        {
            var admin = _factory.CreateClient().AsAdmin();
            var professor = _factory.Services.GetRequiredService<IUserRepository>().FindByUsername("professor")!;

            (await admin.PostAsync($"/api/accounts/{professor.Id}/decline", null)).StatusCode.Should().Be(HttpStatusCode.NotFound);
            (await LoginAsync("professor", "password")).StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task SignUp_WithAnEmailInUse_Returns409OnTheEmailField()
        {
            (await SignUpAsync(ValidSignUp())).StatusCode.Should().Be(HttpStatusCode.Created);

            var again = await SignUpAsync(ValidSignUp(" Ada@Lab.Example "));

            again.StatusCode.Should().Be(HttpStatusCode.Conflict);
            var body = await again.Content.ReadFromJsonAsync<JsonElement>();
            body.GetProperty("errors").GetProperty("email")[0].GetString().Should().Contain("already exists");
        }

        [Fact]
        public async Task SignUp_ReportsEveryProblemByField()
        {
            var response = await SignUpAsync(new SignUpRequest { Email = "not-an-email", Password = "short", DateOfBirth = "2020-01-01" });

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            var errors = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
            errors.EnumerateObject().Select(e => e.Name).Should()
                .BeEquivalentTo("name", "email", "password", "dateOfBirth", "intendedUse", "organization");
            errors.GetProperty("dateOfBirth")[0].GetString().Should().Be("You must be at least 18 to sign up.");
        }

        [Fact]
        public async Task WithoutApproval_NewProfessorsCanSignInStraightAway()
        {
            using var factory = new InMemoryApiFactory();
            var withoutApproval = factory.WithWebHostBuilder(b => b.UseSetting(AuthEndpoints.RequireApprovalSetting, "false"));
            var client = withoutApproval.CreateClient();

            var response = await client.PostAsJsonAsync("/api/auth/signup", ValidSignUp());

            (await response.Content.ReadFromJsonAsync<SignUpResponse>())!.Status.Should().Be("active");
            await InMemoryApiFactory.VerifyEmailAsync(client, withoutApproval.Services, "ada@lab.example");
            (await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Username = "ada@lab.example", Password = "analytical" }))
                .StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task WithoutEmailVerification_ApprovalIsTheOnlyStep()
        {
            using var factory = new InMemoryApiFactory();
            var client = factory.WithWebHostBuilder(b => b.UseSetting(AuthEndpoints.RequireEmailVerificationSetting, "false")).CreateClient();

            var created = await (await client.PostAsJsonAsync("/api/auth/signup", ValidSignUp())).Content.ReadFromJsonAsync<SignUpResponse>();

            created!.EmailVerificationRequired.Should().BeFalse();
            var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Username = "ada@lab.example", Password = "analytical" });
            (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("reason").GetString().Should().Be(SignInBlocks.Pending);
        }
    }
}
