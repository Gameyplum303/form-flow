using System.Net.Http.Json;
using System.Text.RegularExpressions;
using FormFlow.Backend.Email;
using FormFlow.Data.Models;
using LiteDB;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FormFlow.Backend.Tests.Endpoints
{
    /// <summary>
    /// Runs the API against a fresh in-memory LiteDB seeded with the sample questions
    /// and the demo survey.
    /// </summary>
    public sealed class InMemoryApiFactory : WebApplicationFactory<Program>
    {
        public const string DemoSurveyTitle = "Student Experience Survey";

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("SeedData:DemoSurvey", "true");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<ILiteDatabase>();
                services.AddSingleton<ILiteDatabase>(_ => new LiteDatabase(new MemoryStream()));
            });
        }

        public static async Task<SurveyDefinition> GetDemoSurveyAsync(HttpClient client)
        {
            var surveys = await client.GetFromJsonAsync<List<SurveyDefinition>>("/api/surveys");
            return surveys!.Single(s => s.Title == DemoSurveyTitle);
        }

        /// <summary>
        /// The token from the newest link to a Blazor page (such as "verify-email") emailed to an address,
        /// read from the outbox the API uses when no mail server is configured.
        /// </summary>
        public static string? EmailedToken(IServiceProvider services, string to, string page)
        {
            var email = services.GetRequiredService<OutboxEmailSender>().Sent
                .FirstOrDefault(e => string.Equals(e.To, to, StringComparison.OrdinalIgnoreCase) && e.Body.Contains($"/{page}?token="));
            return email is null ? null : Uri.UnescapeDataString(Regex.Match(email.Body, $@"/{page}\?token=(\S+)").Groups[1].Value);
        }

        /// <summary>Opens the verification link emailed to a new sign-up.</summary>
        public static async Task VerifyEmailAsync(HttpClient client, IServiceProvider services, string email)
        {
            var token = EmailedToken(services, email, "verify-email");
            var response = await client.PostAsJsonAsync("/api/auth/verify-email", new VerifyEmailRequest { Token = token! });
            response.EnsureSuccessStatusCode();
        }

        public static async Task<QuestionDefinition> GetQuestionAsync(HttpClient client, string key)
        {
            var questions = await client.GetFromJsonAsync<List<QuestionDefinition>>("/api/questions");
            return questions!.Single(q => q.Key == key);
        }
    }
}
