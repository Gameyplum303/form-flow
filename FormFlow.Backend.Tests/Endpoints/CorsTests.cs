using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace FormFlow.Backend.Tests.Endpoints
{
    /// <summary>
    /// Browsers may call the API from the origins in Cors:AllowedOrigins, where the React app runs.
    /// Development allows any origin.
    /// </summary>
    public class CorsTests : IDisposable
    {
        private const string ReactApp = "https://react.example";

        private readonly InMemoryApiFactory _factory = new();

        public void Dispose() => _factory.Dispose();

        private WebApplicationFactory<Program> InEnvironment(string environment) => _factory.WithWebHostBuilder(b => b
            .UseEnvironment(environment)
            .UseSetting("DisableHttpsRedirection", "true")
            .UseSetting("Cors:AllowedOrigins:0", ReactApp));

        private static async Task<string?> AllowedOriginFor(WebApplicationFactory<Program> factory, string origin)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "/api/surveys");
            request.Headers.Add("Origin", origin);
            var response = await factory.CreateClient().SendAsync(request);
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            return response.Headers.TryGetValues("Access-Control-Allow-Origin", out var values) ? values.Single() : null;
        }

        [Fact]
        public async Task Production_AllowsOnlyTheConfiguredOrigins()
        {
            var production = InEnvironment("Production");

            (await AllowedOriginFor(production, ReactApp)).Should().Be(ReactApp);
            (await AllowedOriginFor(production, "https://elsewhere.example")).Should().BeNull();
        }

        [Fact]
        public async Task Development_AllowsAnyOrigin()
        {
            (await AllowedOriginFor(InEnvironment("Development"), "https://elsewhere.example")).Should().Be("*");
        }
    }
}
