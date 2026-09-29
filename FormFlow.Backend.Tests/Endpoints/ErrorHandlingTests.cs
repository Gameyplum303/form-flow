using System.Net;
using FluentAssertions;
using FormFlow.Backend.Repositories;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;

namespace FormFlow.Backend.Tests.Endpoints
{
    /// <summary>An unexpected error is logged and answered with problem details, without the exception's details.</summary>
    public class ErrorHandlingTests : IDisposable
    {
        private readonly InMemoryApiFactory _factory = new();

        public void Dispose() => _factory.Dispose();

        [Fact]
        public async Task UnhandledError_OutsideDevelopment_ReturnsProblemDetails()
        {
            var broken = new Mock<IQuestionRepository>();
            broken.Setup(r => r.FindAll()).Throws(new InvalidOperationException("secret database detail"));
            var client = _factory.WithWebHostBuilder(b => b
                .UseEnvironment("Production")
                .UseSetting("DisableHttpsRedirection", "true")
                .ConfigureTestServices(services =>
                {
                    services.RemoveAll<IQuestionRepository>();
                    services.AddSingleton(broken.Object);
                })).CreateClient();

            var response = await client.GetAsync("/api/questions");

            response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
            response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
            (await response.Content.ReadAsStringAsync()).Should().NotContain("secret database detail");
        }
    }
}
