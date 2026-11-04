using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using FormFlow.Backend.Services;
using FormFlow.Data.Models;
using FormFlow.Data.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace FormFlow.Backend.Tests.Endpoints
{
    /// <summary>
    /// Quizzes: answer keys on questions that never reach respondents, the quiz settings on surveys,
    /// grading on submit, and the scores in results and the CSV export.
    /// </summary>
    public class QuizEndpointTests : IDisposable
    {
        private const string Explanation = "Paris has been the capital since the tenth century.";

        private readonly InMemoryApiFactory _root = new();
        private readonly WebApplicationFactory<Program> _factory;

        public QuizEndpointTests()
        {
            // A second professor, who must not see the first one's answer keys.
            _factory = _root.WithWebHostBuilder(b => b
                .UseSetting("Accounts:3:Username", "scientist")
                .UseSetting("Accounts:3:Password", "password")
                .UseSetting("Accounts:3:Role", "professor"));
        }

        public void Dispose()
        {
            _factory.Dispose();
            _root.Dispose();
        }

        private HttpClient Professor() => _factory.CreateClient().AsProfessor();
        private HttpClient Scientist() => _factory.CreateClient().SignedInAs("scientist", "password");
        private HttpClient Admin() => _factory.CreateClient().AsAdmin();
        private HttpClient Anonymous() => _factory.CreateClient();

        private static readonly List<Option> Cities = [new() { Value = "paris", Label = "Paris" }, new() { Value = "lyon", Label = "Lyon" }];

        private static readonly List<Option> Numbers =
            [new() { Value = "2", Label = "Two" }, new() { Value = "3", Label = "Three" }, new() { Value = "4", Label = "Four" }];

        private static NewQuestion Capital() => new()
        {
            Key = "capital",
            Label = "What is the capital of France?",
            Type = QuestionTypes.Radio,
            Options = Cities,
            AnswerKey = new AnswerKey { CorrectAnswers = ["paris"], Points = 2, Explanation = Explanation },
        };

        private static async Task<QuestionDefinition> CreateQuestionAsync(HttpClient client, NewQuestion question)
        {
            var response = await client.PostAsJsonAsync("/api/questions", question);
            response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
            return (await response.Content.ReadFromJsonAsync<QuestionDefinition>())!;
        }

        private static async Task<SurveyDefinition> CreateQuizAsync(HttpClient client, string feedback, params Guid[] questionIds)
        {
            var response = await client.PostAsJsonAsync("/api/surveys", new NewSurvey
            {
                Title = "Geography quiz",
                Description = "Five minutes, no notes.",
                QuestionIds = questionIds.ToList(),
                IsQuiz = true,
                QuizFeedback = feedback,
            });
            response.StatusCode.Should().Be(HttpStatusCode.Created);
            var survey = (await response.Content.ReadFromJsonAsync<SurveyDefinition>())!;
            (await client.PutAsJsonAsync($"/api/surveys/{survey.Id}/sharing",
                new SurveySharing { Status = SurveyStatuses.Published, Listed = true })).EnsureSuccessStatusCode();
            return survey;
        }

        /// <summary>A quiz of four questions worth 9 points, one of them shown only to people who know some maths.</summary>
        private async Task<SurveyDefinition> FourQuestionQuizAsync(HttpClient professor, string feedback = QuizFeedbackModes.Answers)
        {
            var capital = await CreateQuestionAsync(professor, Capital());
            var knowsMaths = await CreateQuestionAsync(professor, new NewQuestion { Key = "knows_maths", Label = "Do you know maths?", Type = QuestionTypes.YesNo });
            var sum = await CreateQuestionAsync(professor, new NewQuestion
            {
                Key = "two_plus_two",
                Label = "What is 2 + 2?",
                Type = QuestionTypes.Number,
                VisibleIf = new VisibleIf { Key = "knows_maths", ShouldEqual = true },
                AnswerKey = new AnswerKey { CorrectAnswers = ["4"], Points = 3 },
            });
            var primes = await CreateQuestionAsync(professor, new NewQuestion
            {
                Key = "primes",
                Label = "Which are prime?",
                Type = QuestionTypes.Multiselect,
                Options = Numbers,
                AnswerKey = new AnswerKey { CorrectAnswers = ["2", "3"], Points = 2 },
            });
            var author = await CreateQuestionAsync(professor, new NewQuestion
            {
                Key = "first_programmer",
                Label = "Who wrote the first program?",
                Type = QuestionTypes.Text,
                Required = false,
                AnswerKey = new AnswerKey { CorrectAnswers = ["Ada Lovelace", "Ada"], Points = 2 },
            });
            return await CreateQuizAsync(professor, feedback, capital.Id, knowsMaths.Id, sum.Id, primes.Id, author.Id);
        }

        private static async Task<SubmittedResponse> SubmitAsync(HttpClient client, Guid surveyId, object answers)
        {
            var response = await client.PostAsJsonAsync($"/api/surveys/{surveyId}/responses", new { answers });
            response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
            return (await response.Content.ReadFromJsonAsync<SubmittedResponse>())!;
        }

        [Fact]
        public async Task AnswerKey_IsSaved_AndReturnedToItsCreator()
        {
            var professor = Professor();
            var created = await CreateQuestionAsync(professor, Capital());

            created.AnswerKey.Should().BeEquivalentTo(new AnswerKey { CorrectAnswers = ["paris"], Points = 2, Explanation = Explanation });
            (await professor.GetFromJsonAsync<QuestionDefinition>($"/api/questions/{created.Id}"))!.AnswerKey!.CorrectAnswers.Should().Equal("paris");
        }

        [Fact]
        public async Task AnswerKey_IsValidatedOnTheServer()
        {
            var question = Capital();
            question.AnswerKey!.CorrectAnswers = ["berlin"];
            question.AnswerKey.Points = -1;

            var response = await Professor().PostAsJsonAsync("/api/questions", question);

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            var body = await response.Content.ReadAsStringAsync();
            body.Should().Contain("The correct answer 'berlin' is not one of the options").And.Contain("Points must be from 0 to 1000");
        }

        [Fact]
        public async Task AnswerKey_CanBeChangedAndRemoved_WithAnExplanationTrimmedOrDropped()
        {
            var professor = Professor();
            var created = await CreateQuestionAsync(professor, Capital());

            var update = Capital();
            update.AnswerKey!.Explanation = "   ";
            var changed = await professor.PutAsJsonAsync($"/api/questions/{created.Id}", update);
            (await changed.Content.ReadFromJsonAsync<QuestionDefinition>())!.AnswerKey!.Explanation.Should().BeNull();

            update.AnswerKey = null;
            var removed = await professor.PutAsJsonAsync($"/api/questions/{created.Id}", update);
            (await removed.Content.ReadFromJsonAsync<QuestionDefinition>())!.AnswerKey.Should().BeNull();
        }

        [Fact]
        public async Task AnswerKeys_NeverReachRespondents_OrBuildersWhoDontManageTheQuestion()
        {
            var professor = Professor();
            var survey = await FourQuestionQuizAsync(professor);
            var capital = await InMemoryApiFactory.GetQuestionAsync(professor, "capital");

            // Respondents load the form from these endpoints, signed out, so nothing about the key is in them.
            foreach (var outsider in new[] { Anonymous(), Scientist() })
            {
                var bodies = new[]
                {
                    await outsider.GetStringAsync("/api/questions"),
                    await outsider.GetStringAsync($"/api/questions/{capital.Id}"),
                    await outsider.GetStringAsync($"/api/surveys/{survey.Id}/questions"),
                    await outsider.GetStringAsync($"/api/surveys/{survey.Id}"),
                    await outsider.GetStringAsync($"/api/share/{survey.ShareCode}"),
                };
                foreach (var body in bodies)
                {
                    body.Should().NotContain("correctAnswers").And.NotContain(Explanation).And.NotContain("Ada Lovelace");
                }
                (await outsider.GetFromJsonAsync<List<QuestionDefinition>>($"/api/surveys/{survey.Id}/questions"))!
                    .Should().OnlyContain(q => q.AnswerKey == null);
            }
        }

        [Fact]
        public async Task AnswerKeys_AreShownToTheirCreator_Administrators_AndTheSurveysManagers()
        {
            var professor = Professor();
            var survey = await FourQuestionQuizAsync(professor);

            foreach (var manager in new[] { professor, Admin() })
            {
                (await manager.GetFromJsonAsync<List<QuestionDefinition>>($"/api/surveys/{survey.Id}/questions"))!
                    .Where(q => q.Key != "knows_maths").Should().OnlyContain(q => q.AnswerKey != null);
                (await InMemoryApiFactory.GetQuestionAsync(manager, "capital")).AnswerKey!.Explanation.Should().Be(Explanation);
            }

            // A professor whose quiz uses someone else's question sees its key on their own survey, which
            // they could see in the respondents' review anyway, but not in the shared question bank.
            var scientist = Scientist();
            var capital = await InMemoryApiFactory.GetQuestionAsync(scientist, "capital");
            var theirs = await CreateQuizAsync(scientist, QuizFeedbackModes.Answers, capital.Id);
            (await scientist.GetFromJsonAsync<List<QuestionDefinition>>($"/api/surveys/{theirs.Id}/questions"))!
                .Single().AnswerKey!.CorrectAnswers.Should().Equal("paris");
            capital.AnswerKey.Should().BeNull();
        }

        [Fact]
        public async Task QuizSettings_AreSavedOnCreateAndEdit_AndDefaultToScoreOnly()
        {
            var professor = Professor();
            var capital = await CreateQuestionAsync(professor, Capital());
            var created = await professor.PostAsJsonAsync("/api/surveys",
                new NewSurvey { Title = "Quiz", Description = "A quiz", QuestionIds = [capital.Id], IsQuiz = true });
            var survey = (await created.Content.ReadFromJsonAsync<SurveyDefinition>())!;
            survey.IsQuiz.Should().BeTrue();
            survey.QuizFeedback.Should().Be(QuizFeedbackModes.Score);

            var edited = await professor.PutAsJsonAsync($"/api/surveys/{survey.Id}",
                new NewSurvey { Title = "Quiz", Description = "A quiz", QuestionIds = [capital.Id], IsQuiz = true, QuizFeedback = QuizFeedbackModes.Answers });
            (await edited.Content.ReadFromJsonAsync<SurveyDefinition>())!.QuizFeedback.Should().Be(QuizFeedbackModes.Answers);

            var off = await professor.PutAsJsonAsync($"/api/surveys/{survey.Id}",
                new NewSurvey { Title = "Quiz", Description = "A quiz", QuestionIds = [capital.Id], IsQuiz = false });
            (await off.Content.ReadFromJsonAsync<SurveyDefinition>())!.IsQuiz.Should().BeFalse();
        }

        [Fact]
        public async Task QuizFeedback_MustBeAKnownMode()
        {
            var professor = Professor();
            var capital = await CreateQuestionAsync(professor, Capital());

            var response = await professor.PostAsJsonAsync("/api/surveys",
                new NewSurvey { Title = "Quiz", Description = "A quiz", QuestionIds = [capital.Id], IsQuiz = true, QuizFeedback = "everything" });

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await response.Content.ReadAsStringAsync()).Should().Contain("quizFeedback must be");
        }

        [Fact]
        public async Task Submitting_AQuiz_StoresTheScore_AndReturnsTheReview()
        {
            var professor = Professor();
            var survey = await FourQuestionQuizAsync(professor);

            var submitted = await SubmitAsync(Anonymous(), survey.Id, new Dictionary<string, object>
            {
                ["capital"] = "paris",
                ["knows_maths"] = true,
                ["two_plus_two"] = 5,
                ["primes"] = new[] { "2" },
                ["first_programmer"] = " ada lovelace ",
            });

            // 2 for the capital, nothing for the sum, half of 2 for one of two primes, and 2 for Ada.
            submitted.Score.Should().Be(5);
            submitted.MaxScore.Should().Be(9);
            submitted.Quiz!.Score.Should().Be(5);
            submitted.Quiz.MaxScore.Should().Be(9);
            submitted.Quiz.Percent.Should().Be(56);
            submitted.Quiz.Questions.Select(q => (q.Key, q.Points, q.Correct)).Should().Equal(
                ("capital", 2m, true), ("two_plus_two", 0m, false), ("primes", 1m, false), ("first_programmer", 2m, true));
            var capital = submitted.Quiz.Questions[0];
            capital.GivenAnswers.Should().Equal("Paris");
            capital.CorrectAnswers.Should().Equal("Paris");
            capital.Explanation.Should().Be(Explanation);
            submitted.Quiz.Questions[2].CorrectAnswers.Should().Equal("Two", "Three");

            var stored = (await professor.GetFromJsonAsync<List<SurveyResponse>>($"/api/surveys/{survey.Id}/responses"))!.Single();
            stored.Score.Should().Be(5);
            stored.MaxScore.Should().Be(9);
        }

        [Fact]
        public async Task HiddenQuestions_DontCountTowardTheMaximum()
        {
            var survey = await FourQuestionQuizAsync(Professor());

            var submitted = await SubmitAsync(Anonymous(), survey.Id, new Dictionary<string, object>
            {
                ["capital"] = "lyon",
                ["knows_maths"] = false,
                ["two_plus_two"] = 4,
                ["primes"] = new[] { "2", "3" },
            });

            submitted.Quiz!.MaxScore.Should().Be(6);
            submitted.Quiz.Score.Should().Be(2);
            submitted.Quiz.Questions.Select(q => q.Key).Should().Equal("capital", "primes", "first_programmer");
        }

        [Fact]
        public async Task ScoreOnlyQuizzes_DontSendTheCorrectAnswers()
        {
            var survey = await FourQuestionQuizAsync(Professor(), QuizFeedbackModes.Score);

            var response = await Anonymous().PostAsJsonAsync($"/api/surveys/{survey.Id}/responses",
                new { answers = new Dictionary<string, object> { ["capital"] = "lyon", ["knows_maths"] = false, ["primes"] = new[] { "4" } } });

            response.StatusCode.Should().Be(HttpStatusCode.Created);
            var body = await response.Content.ReadAsStringAsync();
            body.Should().NotContain(Explanation).And.NotContain("correctAnswers").And.NotContain("Paris");
            var submitted = (await response.Content.ReadFromJsonAsync<SubmittedResponse>())!;
            submitted.Quiz!.Score.Should().Be(0);
            submitted.Quiz.MaxScore.Should().Be(6);
            submitted.Quiz.Questions.Should().BeEmpty();
        }

        [Fact]
        public async Task Surveys_ThatArentQuizzes_AreNotGraded()
        {
            var admin = Admin();
            var demo = await InMemoryApiFactory.GetDemoSurveyAsync(admin);

            var response = await admin.PostAsJsonAsync($"/api/surveys/{demo.Id}/responses", ResponseEndpointTests.ValidAnswers());
            response.StatusCode.Should().Be(HttpStatusCode.Created);
            var submitted = (await response.Content.ReadFromJsonAsync<SubmittedResponse>())!;

            submitted.Quiz.Should().BeNull();
            submitted.Score.Should().BeNull();
            (await admin.GetFromJsonAsync<SurveyResults>($"/api/surveys/{demo.Id}/results"))!.Quiz.Should().BeNull();
        }

        [Fact]
        public async Task ChangingAnAnswerKey_DoesNotRegradeStoredScores()
        {
            var professor = Professor();
            var survey = await FourQuestionQuizAsync(professor);
            await SubmitAsync(Anonymous(), survey.Id, new Dictionary<string, object> { ["capital"] = "lyon", ["knows_maths"] = false, ["primes"] = new[] { "4" } });

            var capital = await InMemoryApiFactory.GetQuestionAsync(professor, "capital");
            var update = Capital();
            update.AnswerKey!.CorrectAnswers = ["lyon"];
            (await professor.PutAsJsonAsync($"/api/questions/{capital.Id}", update)).EnsureSuccessStatusCode();

            var stored = (await professor.GetFromJsonAsync<List<SurveyResponse>>($"/api/surveys/{survey.Id}/responses"))!.Single();
            stored.Score.Should().Be(0);
        }

        [Fact]
        public async Task Results_SummarizeTheScores_AndFollowTheFilters()
        {
            var professor = Professor();
            var survey = await FourQuestionQuizAsync(professor);
            var anonymous = Anonymous();
            // 9 of 9, 5 of 9 and 2 of 6.
            await SubmitAsync(anonymous, survey.Id, new Dictionary<string, object>
            {
                ["capital"] = "paris",
                ["knows_maths"] = true,
                ["two_plus_two"] = 4,
                ["primes"] = new[] { "2", "3" },
                ["first_programmer"] = "Ada",
            });
            await SubmitAsync(anonymous, survey.Id, new Dictionary<string, object>
            {
                ["capital"] = "paris",
                ["knows_maths"] = true,
                ["two_plus_two"] = 5,
                ["primes"] = new[] { "2" },
                ["first_programmer"] = "Ada",
            });
            await SubmitAsync(anonymous, survey.Id, new Dictionary<string, object>
            {
                ["capital"] = "lyon",
                ["knows_maths"] = false,
                ["primes"] = new[] { "2", "3" },
            });

            var quiz = (await professor.GetFromJsonAsync<SurveyResults>($"/api/surveys/{survey.Id}/results"))!.Quiz!;

            quiz.GradedResponses.Should().Be(3);
            quiz.AverageScore.Should().Be(5.33m);
            quiz.AverageMaxScore.Should().Be(8);
            quiz.AveragePercent.Should().Be(63.0m);
            quiz.Distribution.Should().HaveCount(10);
            quiz.Distribution.Where(b => b.Count > 0).Select(b => (b.Label, b.Count)).Should().Equal(("30–39%", 1), ("50–59%", 1), ("90–100%", 1));
            quiz.Questions.Select(q => (q.Key, q.Graded, q.Correct, q.PercentCorrect)).Should().Equal(
                ("capital", 3, 2, 67), ("two_plus_two", 2, 1, 50), ("primes", 3, 2, 67), ("first_programmer", 3, 2, 67));
            quiz.Questions.Single(q => q.Key == "primes").AveragePoints.Should().Be(1.67m);

            var students = (await professor.GetFromJsonAsync<SurveyResults>($"/api/surveys/{survey.Id}/results?filter=knows_maths:true"))!.Quiz!;
            students.GradedResponses.Should().Be(2);
            students.AverageScore.Should().Be(7);
        }

        [Fact]
        public async Task CsvExport_HasScoreColumns_ForQuizzesOnly()
        {
            var professor = Professor();
            var survey = await FourQuestionQuizAsync(professor);
            await SubmitAsync(Anonymous(), survey.Id, new Dictionary<string, object> { ["capital"] = "paris", ["knows_maths"] = false, ["primes"] = new[] { "2" } });

            var lines = (await professor.GetStringAsync($"/api/surveys/{survey.Id}/responses/export")).Split("\r\n");

            lines[0].Should().StartWith("response_id,submitted_at,submitted_by,score,max_score,capital,");
            lines[1].Split(',')[3..5].Should().Equal("3", "6");

            var admin = Admin();
            var demo = await InMemoryApiFactory.GetDemoSurveyAsync(admin);
            (await admin.GetStringAsync($"/api/surveys/{demo.Id}/responses/export")).Should().StartWith("response_id,submitted_at,submitted_by,first_name");
        }

        [Fact]
        public async Task Duplicating_AQuiz_KeepsItsSettings()
        {
            var professor = Professor();
            var survey = await FourQuestionQuizAsync(professor);

            var response = await professor.PostAsync($"/api/surveys/{survey.Id}/duplicate", null);

            var copy = (await response.Content.ReadFromJsonAsync<SurveyDefinition>())!;
            copy.IsQuiz.Should().BeTrue();
            copy.QuizFeedback.Should().Be(QuizFeedbackModes.Answers);
        }

        [Fact]
        public async Task TheQuizTemplate_MakesAGradedQuiz()
        {
            var professor = Professor();
            var templates = (await professor.GetFromJsonAsync<List<SurveyTemplate>>("/api/templates"))!;
            templates.Should().ContainSingle(t => t.IsQuiz).Which.Title.Should().Be("Quiz");

            var created = await professor.PostAsync($"/api/templates/{templates.Single(t => t.IsQuiz).Id}/use", null);
            var survey = (await created.Content.ReadFromJsonAsync<SurveyDefinition>())!;
            survey.IsQuiz.Should().BeTrue();
            survey.QuizFeedback.Should().Be(QuizFeedbackModes.Answers);
            (await professor.PutAsJsonAsync($"/api/surveys/{survey.Id}/sharing", new SurveySharing { Status = SurveyStatuses.Published })).EnsureSuccessStatusCode();

            var submitted = await SubmitAsync(Anonymous(), survey.Id, new Dictionary<string, object>
            {
                ["quiz_cpu"] = "cpu",
                ["quiz_byte"] = "8",
                ["quiz_binary"] = 10,
                ["quiz_ram_volatile"] = true,
                ["quiz_languages"] = new[] { "python", "java", "csharp" },
                ["quiz_html"] = "hypertext",
            });
            submitted.Quiz!.Score.Should().Be(7);
            submitted.Quiz.Percent.Should().Be(100);
            submitted.Quiz.Questions.Should().HaveCount(6).And.OnlyContain(q => q.Correct && q.Explanation != null);
        }

        [Fact]
        public async Task SampleResponses_ForAQuiz_AreGraded()
        {
            var professor = Professor();
            var survey = await FourQuestionQuizAsync(professor);
            var questions = (await professor.GetFromJsonAsync<List<QuestionDefinition>>($"/api/surveys/{survey.Id}/questions"))!;
            var validator = _factory.Services.GetRequiredService<ResponseValidator>();

            var sample = SampleResponseGenerator.Generate(survey, questions, validator, 10, DateTime.UtcNow);

            sample.Should().HaveCount(10).And.OnlyContain(r => r.Score != null && r.MaxScore >= 6 && r.Score <= r.MaxScore);
        }
    }
}
