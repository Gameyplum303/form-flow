using System.Net;
using System.Text;
using FluentAssertions;
using FormFlow.Blazor.Services;
using RichardSzalay.MockHttp;

namespace FormFlow.Blazor.Tests.Auth;

public class AuthServiceTests
{
    private readonly MockHttpMessageHandler _http = new();
    private readonly AuthService _service;

    public AuthServiceTests()
    {
        _service = new AuthService(new HttpClient(_http) { BaseAddress = new Uri("http://api.test/") });
    }

    [Fact]
    public async Task LoginAsync_returns_the_token()
    {
        _http.When(HttpMethod.Post, "http://api.test/api/auth/login")
            .WithContent("""{"username":"admin","password":"secret"}""")
            .Respond(new StringContent("""{"token":"abc","username":"admin","expiresAt":"2030-01-01T00:00:00Z"}""", Encoding.UTF8, "application/json"));

        var (login, error) = await _service.LoginAsync("admin", "secret");

        error.Should().BeNull();
        login!.Token.Should().Be("abc");
        login.Username.Should().Be("admin");
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "Invalid username or password.")]
    [InlineData(HttpStatusCode.Forbidden, "Your account is waiting for an administrator's approval.")]
    [InlineData(HttpStatusCode.TooManyRequests, "Too many sign-in attempts. Wait a minute and try again.")]
    [InlineData(HttpStatusCode.InternalServerError, "Sign-in failed (500). Please try again.")]
    public async Task LoginAsync_explains_failures(HttpStatusCode status, string expected)
    {
        _http.When(HttpMethod.Post, "http://api.test/api/auth/login").Respond(status);

        var (login, error) = await _service.LoginAsync("admin", "wrong");

        login.Should().BeNull();
        error.Should().Be(expected);
    }

    [Fact]
    public async Task LoginAsync_reports_an_unreachable_server()
    {
        _http.When(HttpMethod.Post, "http://api.test/api/auth/login").Throw(new HttpRequestException("down"));

        var (_, error) = await _service.LoginAsync("admin", "secret");

        error.Should().Be("Could not reach the server. Please try again.");
    }

    private static FormFlow.Data.Models.SignUpRequest SignUp() => new()
    {
        Name = "Ada",
        Email = "ada@lab.example",
        Password = "analytical",
        DateOfBirth = "1990-12-10",
        IntendedUse = "Studies",
        Organization = "Lab",
    };

    [Fact]
    public async Task SignUpAsync_returns_the_new_accounts_status()
    {
        _http.When(HttpMethod.Post, "http://api.test/api/auth/signup")
            .Respond(HttpStatusCode.Created, new StringContent("""{"status":"pending"}""", Encoding.UTF8, "application/json"));

        var result = await _service.SignUpAsync(SignUp());

        result.Succeeded.Should().BeTrue();
        result.Status.Should().Be("pending");
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Conflict)]
    public async Task SignUpAsync_returns_problems_by_field(HttpStatusCode status)
    {
        _http.When(HttpMethod.Post, "http://api.test/api/auth/signup")
            .Respond(status, new StringContent("""{"title":"One or more validation errors occurred.","errors":{"email":["Taken."],"name":["Enter your name."]}}""",
                Encoding.UTF8, "application/problem+json"));

        var result = await _service.SignUpAsync(SignUp());

        result.Succeeded.Should().BeFalse();
        result.FieldErrors["email"].Should().Equal("Taken.");
        result.FieldErrors["name"].Should().Equal("Enter your name.");
        result.Error.Should().BeNull();
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests, "Too many attempts. Wait a minute and try again.")]
    [InlineData(HttpStatusCode.InternalServerError, "Sign-up failed (500). Please try again.")]
    public async Task SignUpAsync_explains_other_failures(HttpStatusCode status, string expected)
    {
        _http.When(HttpMethod.Post, "http://api.test/api/auth/signup").Respond(status);

        var result = await _service.SignUpAsync(SignUp());

        result.Error.Should().Be(expected);
        result.FieldErrors.Should().BeEmpty();
    }

    private static StringContent Json(string json) => new(json, Encoding.UTF8, "application/json");

    [Fact]
    public async Task LoginAsync_says_when_the_email_address_needs_verifying()
    {
        _http.When(HttpMethod.Post, "http://api.test/api/auth/login")
            .Respond(HttpStatusCode.Forbidden, Json("""{"title":"Please verify your email address first.","status":403,"reason":"email_unverified"}"""));

        var (_, error) = await _service.LoginAsync("ada@lab.example", "analytical");

        error.Should().Be(AuthService.VerifyEmailMessage);
    }

    [Fact]
    public async Task SignUpAsync_says_whether_the_email_needs_verifying()
    {
        _http.When(HttpMethod.Post, "http://api.test/api/auth/signup")
            .Respond(HttpStatusCode.Created, Json("""{"status":"pending","emailVerificationRequired":true}"""));

        (await _service.SignUpAsync(SignUp())).EmailVerificationRequired.Should().BeTrue();
    }

    [Theory]
    [InlineData("forgot-password")]
    [InlineData("resend-verification")]
    public async Task Email_requests_send_the_trimmed_address(string endpoint)
    {
        _http.Expect(HttpMethod.Post, $"http://api.test/api/auth/{endpoint}")
            .WithContent("""{"email":"ada@lab.example"}""")
            .Respond(HttpStatusCode.Accepted, Json("""{"message":"sent"}"""));

        var error = endpoint == "forgot-password"
            ? await _service.RequestPasswordResetAsync(" ada@lab.example ")
            : await _service.ResendVerificationAsync(" ada@lab.example ");

        error.Should().BeNull();
        _http.VerifyNoOutstandingExpectation();
    }

    [Fact]
    public async Task Email_requests_explain_the_rate_limit()
    {
        _http.When(HttpMethod.Post, "http://api.test/api/auth/forgot-password").Respond(HttpStatusCode.TooManyRequests);

        (await _service.RequestPasswordResetAsync("ada@lab.example")).Should().Be("Too many attempts. Wait a minute and try again.");
    }

    [Fact]
    public async Task VerifyEmailAsync_returns_the_status_or_explains_a_bad_link()
    {
        _http.When(HttpMethod.Post, "http://api.test/api/auth/verify-email").WithContent("""{"token":"good"}""")
            .Respond(Json("""{"status":"pending"}"""));
        _http.When(HttpMethod.Post, "http://api.test/api/auth/verify-email").WithContent("""{"token":"used"}""")
            .Respond(HttpStatusCode.BadRequest, Json("""{"title":"This link is invalid or has expired. Ask for a new one."}"""));

        (await _service.VerifyEmailAsync("good")).Should().Be(("pending", (string?)null));
        (await _service.VerifyEmailAsync("used")).Should().Be(((string?)null, AuthService.InvalidLinkMessage));
    }

    [Fact]
    public async Task ResetPasswordAsync_tells_a_bad_password_from_a_bad_link()
    {
        _http.When(HttpMethod.Post, "http://api.test/api/auth/reset-password").WithPartialContent("\"password\":\"short\"")
            .Respond(HttpStatusCode.BadRequest, Json("""{"errors":{"password":["Use at least 8 characters."]}}"""));
        _http.When(HttpMethod.Post, "http://api.test/api/auth/reset-password").WithPartialContent("\"token\":\"used\"")
            .Respond(HttpStatusCode.BadRequest, Json("""{"title":"This link is invalid or has expired. Ask for a new one."}"""));
        _http.When(HttpMethod.Post, "http://api.test/api/auth/reset-password").Respond(HttpStatusCode.NoContent);

        (await _service.ResetPasswordAsync("good", "short")).FieldErrors["password"].Should().Equal("Use at least 8 characters.");
        (await _service.ResetPasswordAsync("used", "long enough")).Error.Should().Be(AuthService.InvalidLinkMessage);
        (await _service.ResetPasswordAsync("good", "long enough")).Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task ChangePasswordAsync_sends_the_sign_in_and_returns_the_new_one()
    {
        var session = new FakeSessionStorage().CreateSession();
        await session.SignInAsync(FakeSessionStorage.Login());
        var service = new AuthService(new HttpClient(_http) { BaseAddress = new Uri("http://api.test/") }, session);
        _http.When(HttpMethod.Post, "http://api.test/api/auth/change-password")
            .WithHeaders("Authorization", "Bearer test-token")
            .WithPartialContent("\"currentPassword\":\"wrong\"")
            .Respond(HttpStatusCode.BadRequest, Json("""{"errors":{"currentPassword":["That isn't your current password."]}}"""));
        _http.When(HttpMethod.Post, "http://api.test/api/auth/change-password")
            .WithHeaders("Authorization", "Bearer test-token")
            .Respond(Json("""{"token":"new-token","username":"admin","role":"admin","expiresAt":"2030-01-01T00:00:00Z"}"""));

        (await service.ChangePasswordAsync("wrong", "a new password")).FieldErrors["currentPassword"]
            .Should().Equal("That isn't your current password.");
        var changed = await service.ChangePasswordAsync("secret", "a new password");
        changed.Succeeded.Should().BeTrue();
        changed.Login!.Token.Should().Be("new-token");
    }

    [Fact]
    public async Task ChangePasswordAsync_explains_an_ended_sign_in()
    {
        _http.When(HttpMethod.Post, "http://api.test/api/auth/change-password").Respond(HttpStatusCode.Unauthorized);

        (await _service.ChangePasswordAsync("secret", "a new password")).Error.Should().StartWith("Your sign-in has ended.");
    }
}
