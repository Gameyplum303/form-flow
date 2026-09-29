using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using FormFlow.Data.Models;

namespace FormFlow.Backend.Tests.Endpoints
{
    /// <summary>
    /// The demo survey's likert grid (campus_services), NPS (recommend_score) and slider (study_hours)
    /// questions, through the question, response, results and export endpoints.
    /// </summary>
    public class ScaleQuestionEndpointTests : IDisposable
    {
        private readonly InMemoryApiFactory _factory = new();
        private readonly HttpClient _client;

        public ScaleQuestionEndpointTests()
        {
            _client = _factory.CreateClient().AsAdmin();
        }

        public void Dispose() => _factory.Dispose();

        private static object Answers(object grid, object nps, object slider) => ResponseEndpointTests.ValidAnswers(a =>
        {
            a["campus_services"] = grid;
            a["recommend_score"] = nps;
            a["study_hours"] = slider;
        });

        private async Task<SurveyDefinition> SubmitAsync(params object[] bodies)
        {
            var survey = await InMemoryApiFactory.GetDemoSurveyAsync(_client);
            foreach (var body in bodies)
            {
                var response = await _client.PostAsJsonAsync($"/api/surveys/{survey.Id}/responses", body);
                response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
            }
            return survey;
        }

        [Fact]
        public async Task DemoSurvey_HasOneOptionalQuestionOfEachType()
        {
            var survey = await InMemoryApiFactory.GetDemoSurveyAsync(_client);
            var questions = await _client.GetFromJsonAsync<List<QuestionDefinition>>($"/api/surveys/{survey.Id}/questions");

            var byType = questions!.Where(q => q.Type is "likert" or "nps" or "slider").ToDictionary(q => q.Type);
            byType.Keys.Should().BeEquivalentTo(["likert", "nps", "slider"]);
            byType.Values.Should().OnlyContain(q => !q.Required);
            byType["likert"].Rows.Select(r => r.Value).Should().Equal("library", "labs", "advising");
            QuestionTypes.SliderRange(byType["slider"]).Should().Be((0m, 40m));
        }

        [Fact]
        public async Task Submit_StoresNormalizedAnswers()
        {
            var survey = await SubmitAsync(Answers(new[] { "advising=2", "library=5" }, "09", 12));

            var stored = (await _client.GetFromJsonAsync<List<SurveyResponse>>($"/api/surveys/{survey.Id}/responses"))!.Single().Answers;
            stored["campus_services"].Should().Equal("library=5", "advising=2");
            stored["recommend_score"].Should().Equal("9");
            stored["study_hours"].Should().Equal("12");
        }

        [Theory]
        [InlineData("campus_services", "gym=3")]
        [InlineData("campus_services", "library=6")]
        [InlineData("recommend_score", "11")]
        [InlineData("study_hours", "41")]
        [InlineData("study_hours", "2.5")]
        public async Task Submit_RejectsBadAnswers(string key, string answer)
        {
            var survey = await InMemoryApiFactory.GetDemoSurveyAsync(_client);

            var response = await _client.PostAsJsonAsync($"/api/surveys/{survey.Id}/responses",
                ResponseEndpointTests.ValidAnswers(a => a[key] = answer));

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await response.Content.ReadAsStringAsync()).Should().Contain(key);
        }

        [Fact]
        public async Task Results_SummarizeRows_NpsAndSliders()
        {
            var survey = await SubmitAsync(
                Answers(new[] { "library=5", "labs=4" }, 10, 10),
                Answers(new[] { "library=4", "labs=2", "advising=3" }, 9, 20),
                Answers(new[] { "library=1" }, 7, 30),
                Answers(new[] { "library=2" }, 3, 40));

            var results = await _client.GetFromJsonAsync<SurveyResults>($"/api/surveys/{survey.Id}/results");
            var byKey = results!.Questions.ToDictionary(q => q.Key);

            var grid = byKey["campus_services"];
            grid.AnsweredCount.Should().Be(4);
            grid.Options.Should().BeEmpty("a grid is summarized per row");
            grid.Rows.Select(r => (r.Label, r.AnsweredCount, r.Average)).Should().Equal(
                ("The library has the resources I need.", 4, 3m), ("Lab equipment is up to date.", 2, 3m), ("Advisors are easy to reach.", 1, 3m));
            grid.Rows[0].Options.Select(o => (o.Label, o.Count)).Should().Equal(
                ("Strongly disagree", 1), ("Disagree", 1), ("Neutral", 0), ("Agree", 1), ("Strongly agree", 1));

            var nps = byKey["recommend_score"];
            nps.Options.Select(o => o.Value).Should().Equal(Enumerable.Range(0, 11).Select(n => n.ToString()));
            nps.Options.Single(o => o.Value == "10").Count.Should().Be(1);
            nps.Nps.Should().BeEquivalentTo(new NpsSummary { Score = 25, Promoters = 2, Passives = 1, Detractors = 1 });

            byKey["study_hours"].Numbers.Should().BeEquivalentTo(new NumberSummary { Min = 10, Max = 40, Average = 25 });
        }

        [Fact]
        public async Task Results_FilterAndCompareByNps_ButNotByGridsOrSliders()
        {
            var survey = await SubmitAsync(
                Answers(new[] { "library=5" }, 10, 10),
                Answers(new[] { "library=1" }, 3, 20));

            var filtered = await _client.GetFromJsonAsync<SurveyResults>(
                $"/api/surveys/{survey.Id}/results?filter=recommend_score:10&compareBy=recommend_score");
            filtered!.MatchingResponses.Should().Be(1);
            filtered.Questions.Single(q => q.Key == "study_hours").Numbers!.Average.Should().Be(10);
            filtered.Comparison!.Groups.Should().HaveCount(11);
            filtered.Comparison.Groups.Single(g => g.Value == "10").Responses.Should().Be(1);

            var byStudent = await _client.GetFromJsonAsync<SurveyResults>($"/api/surveys/{survey.Id}/results?compareBy=is_student");
            byStudent!.Comparison!.Groups[0].Questions.Single(q => q.Key == "recommend_score").Nps!.Score.Should().Be(0);
            byStudent.Comparison.Groups[0].Questions.Single(q => q.Key == "campus_services").Rows[0].Average.Should().Be(3);

            (await _client.GetAsync($"/api/surveys/{survey.Id}/results?compareBy=campus_services")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await _client.GetAsync($"/api/surveys/{survey.Id}/results?filter=study_hours:10")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task Export_GivesEachGridRowItsOwnColumn()
        {
            var survey = await SubmitAsync(Answers(new[] { "library=5", "advising=2" }, 8, 15));

            var csv = await _client.GetStringAsync($"/api/surveys/{survey.Id}/responses/export");
            var lines = csv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
            var header = lines[0].Split(',').ToList();
            var row = lines[1].Split(',');

            header.Should().ContainInOrder("experience_rating",
                "campus_services[library]", "campus_services[labs]", "campus_services[advising]",
                "recommend_score", "study_hours", "comments");
            header.Should().NotContain("campus_services");
            row[header.IndexOf("campus_services[library]")].Should().Be("5");
            row[header.IndexOf("campus_services[labs]")].Should().BeEmpty();
            row[header.IndexOf("campus_services[advising]")].Should().Be("2");
            row[header.IndexOf("recommend_score")].Should().Be("8");
            row[header.IndexOf("study_hours")].Should().Be("15");
        }

        [Fact]
        public async Task CreateQuestion_KeepsALikertGridsRows_AndDropsRowsFromOtherTypes()
        {
            var rows = new List<Option> { new() { Label = "Clear", Value = "clear" }, new() { Label = "Useful", Value = "useful" } };
            var grid = await _client.PostAsJsonAsync("/api/questions",
                new NewQuestion { Key = "lecture", Label = "The lecture was…", Type = "likert", Rows = rows });
            var text = await _client.PostAsJsonAsync("/api/questions",
                new NewQuestion { Key = "note", Label = "Note", Type = "text", Rows = rows });

            grid.StatusCode.Should().Be(HttpStatusCode.Created);
            var saved = await InMemoryApiFactory.GetQuestionAsync(_client, "lecture");
            saved.Rows.Select(r => r.Value).Should().Equal("clear", "useful");
            saved.Options.Should().BeEmpty();
            (await InMemoryApiFactory.GetQuestionAsync(_client, "note")).Rows.Should().BeEmpty();
            (await text.Content.ReadAsStringAsync()).Should().Contain("\"rows\":[]", "the JSON uses camelCase");
        }

        [Theory]
        [InlineData("likert", null, "A likert grid needs at least one row")]
        [InlineData("slider", """[{"validationType":"MinValue","minValue":50},{"validationType":"MaxValue","maxValue":10}]""", "A slider needs whole-number minimum and maximum values")]
        public async Task CreateQuestion_RejectsBadGridsAndSliders(string type, string? rules, string message)
        {
            var response = await _client.PostAsJsonAsync("/api/questions",
                new NewQuestion { Key = "bad", Label = "Bad", Type = type, ValidationConfigs = rules });

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await response.Content.ReadAsStringAsync()).Should().Contain(message);
        }
    }
}
