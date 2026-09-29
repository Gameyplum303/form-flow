using System.Net.Http.Json;
using FluentAssertions;
using FormFlow.Backend.Services;
using FormFlow.Data.Models;
using Microsoft.AspNetCore.Hosting;

namespace FormFlow.Backend.Tests.Endpoints
{
    /// <summary>SeedData:SampleResponses gives a fresh demo results to explore.</summary>
    public class SampleResponseTests
    {
        private static async Task<(SurveyResults Results, List<SurveyResponse> Responses)> SeededAsync(string count)
        {
            using var factory = new InMemoryApiFactory();
            var client = factory.WithWebHostBuilder(b => b.UseSetting("SeedData:SampleResponses", count)).CreateClient().AsAdmin();
            var survey = await InMemoryApiFactory.GetDemoSurveyAsync(client);
            var results = await client.GetFromJsonAsync<SurveyResults>($"/api/surveys/{survey.Id}/results");
            var responses = await client.GetFromJsonAsync<List<SurveyResponse>>($"/api/surveys/{survey.Id}/responses");
            return (results!, responses!);
        }

        [Fact]
        public async Task SampleResponses_AreValid_AndSpreadOverThePastThreeWeeks()
        {
            var (results, responses) = await SeededAsync("60");

            results.TotalResponses.Should().Be(60);
            responses.Should().OnlyContain(r => r.SubmittedAt > DateTime.UtcNow.AddDays(-SampleResponseGenerator.Days - 1));
            results.Timeline.Count.Should().BeGreaterThan(14, "the answers arrive over about three weeks");
            responses.Where(r => r.Answers["is_student"][0] == "false")
                .Should().OnlyContain(r => !r.Answers.ContainsKey("campus_preference"), "the campus question is only for students");
            var student = results.Questions.Single(q => q.Key == "is_student").Options;
            student.Single(o => o.Value == "true").Count.Should().BeGreaterThan(student.Single(o => o.Value == "false").Count);
        }

        [Fact]
        public async Task WithoutTheSetting_TheDemoSurveyStartsEmpty()
        {
            var (results, _) = await SeededAsync("0");

            results.TotalResponses.Should().Be(0);
        }
    }
}
