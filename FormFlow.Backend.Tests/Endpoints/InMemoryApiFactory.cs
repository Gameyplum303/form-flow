using System.Net.Http.Json;
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

        public static async Task<QuestionDefinition> GetQuestionAsync(HttpClient client, string key)
        {
            var questions = await client.GetFromJsonAsync<List<QuestionDefinition>>("/api/questions");
            return questions!.Single(q => q.Key == key);
        }
    }
}
