using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using FormFlow.Backend.Repositories;
using FormFlow.Data.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace FormFlow.Backend.Tests.Endpoints
{
    /// <summary>Filtering, date ranges, the timeline and group comparisons on the results and CSV endpoints.</summary>
    public class ResultsAnalyticsTests : IDisposable
    {
        private readonly InMemoryApiFactory _factory = new();
        private readonly HttpClient _client;

        public ResultsAnalyticsTests()
        {
            _client = _factory.CreateClient().AsAdmin();
        }

        public void Dispose() => _factory.Dispose();

        private async Task<SurveyDefinition> SurveyWithResponsesAsync()
        {
            var survey = await InMemoryApiFactory.GetDemoSurveyAsync(_client);
            await _client.PostAsJsonAsync($"/api/surveys/{survey.Id}/responses", ResponseEndpointTests.ValidAnswers());
            await _client.PostAsJsonAsync($"/api/surveys/{survey.Id}/responses", ResponseEndpointTests.ValidAnswers(a =>
            {
                a["first_name"] = "Grace";
                a["age"] = 40;
                a["is_student"] = false;
                a["skills"] = new[] { "csharp" };
                a.Remove("campus_preference");
            }));
            return survey;
        }

        [Fact]
        public async Task Results_FilterByAnswer_AndCompareGroups()
        {
            var survey = await SurveyWithResponsesAsync();

            var filtered = await _client.GetFromJsonAsync<SurveyResults>(
                $"/api/surveys/{survey.Id}/results?filter=skills:sql&compareBy=is_student");

            filtered!.TotalResponses.Should().Be(2);
            filtered.MatchingResponses.Should().Be(1);
            filtered.Questions.Single(q => q.Key == "age").Numbers!.Average.Should().Be(28);
            filtered.Comparison!.Groups.Select(g => (g.Label, g.Responses)).Should().Equal(("Yes", 1), ("No", 0));

            var compared = await _client.GetFromJsonAsync<SurveyResults>($"/api/surveys/{survey.Id}/results?compareBy=is_student");
            compared!.Comparison!.Groups[1].Questions.Single(q => q.Key == "age").Numbers!.Average.Should().Be(40);
            compared.Timeline.Should().ContainSingle().Which.Count.Should().Be(2);
        }

        [Fact]
        public async Task Results_DateRange_UsesTheSubmissionTimes()
        {
            var survey = await InMemoryApiFactory.GetDemoSurveyAsync(_client);
            var responses = _factory.Services.GetRequiredService<IResponseRepository>();
            foreach (var day in new[] { 1, 2, 20 })
            {
                responses.Insert(new SurveyResponse
                {
                    Id = Guid.NewGuid(),
                    SurveyId = survey.Id,
                    SubmittedAt = new DateTime(2026, 9, day, 15, 0, 0, DateTimeKind.Utc),
                    Answers = new() { ["is_student"] = ["true"] }
                });
            }

            var results = await _client.GetFromJsonAsync<SurveyResults>(
                $"/api/surveys/{survey.Id}/results?from=2026-09-02T00:00:00Z&to=2026-09-21T00:00:00Z&utcOffset=-300");

            results!.MatchingResponses.Should().Be(2);
            results.Timeline.First().Should().BeEquivalentTo(new TimelinePoint { Start = "2026-09-01", Count = 0 },
                "midnight UTC on the 2nd is still the 1st five hours behind UTC");
            results.Timeline.Last().Should().BeEquivalentTo(new TimelinePoint { Start = "2026-09-20", Count = 1 });
        }

        [Theory]
        [InlineData("filter=first_name:Ada", "filter", "Only yes/no, choice and rating questions")]
        [InlineData("filter=nope:1", "filter", "no question with the key 'nope'")]
        [InlineData("filter=is_student:maybe", "filter", "'maybe' isn't one of the answers")]
        [InlineData("filter=is_student", "filter", "should be key:value")]
        [InlineData("compareBy=age", "compareBy", "Only yes/no, choice and rating questions")]
        [InlineData("from=yesterday", "from", "ISO 8601")]
        [InlineData("from=2026-09-02&to=2026-09-01", "to", "must be after")]
        [InlineData("utcOffset=9999", "utcOffset", "between -840 and 840")]
        public async Task Results_RejectBadParameters_ByField(string queryString, string field, string message)
        {
            var survey = await InMemoryApiFactory.GetDemoSurveyAsync(_client);

            var response = await _client.GetAsync($"/api/surveys/{survey.Id}/results?{queryString}");

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
            problem!.Errors[field].Should().ContainSingle().Which.Should().Contain(message);
        }

        [Fact]
        public async Task Export_OnlyIncludesTheFilteredResponses()
        {
            var survey = await SurveyWithResponsesAsync();

            var all = await _client.GetStringAsync($"/api/surveys/{survey.Id}/responses/export");
            var students = await _client.GetStringAsync($"/api/surveys/{survey.Id}/responses/export?filter=is_student:true");

            all.Should().Contain("Grace").And.Contain("Ada");
            students.Should().Contain("Ada").And.NotContain("Grace");
            (await _client.GetAsync($"/api/surveys/{survey.Id}/responses/export?filter=bad")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public void QueryString_RoundTripsThroughTheApi()
        {
            var query = new ResultsQuery
            {
                Filters = [new("skills", "c#")],
                From = new DateTime(2026, 9, 2, 4, 0, 0, DateTimeKind.Utc),
                CompareBy = "is_student",
                UtcOffsetMinutes = -240
            };

            query.ToQueryString().Should().Be("filter=skills%3Ac%23&from=2026-09-02T04%3A00%3A00Z&compareBy=is_student&utcOffset=-240");
            new ResultsQuery().ToQueryString().Should().BeEmpty();
        }
    }
}
