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
}
