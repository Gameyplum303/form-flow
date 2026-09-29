using System.Globalization;
using FormFlow.Data.Models;
using FormFlow.Data.Services;

namespace FormFlow.Backend.Services
{
    /// <summary>
    /// Makes believable answers to a survey, so a fresh demo has results, charts and a timeline to
    /// explore. Every response goes through <see cref="ResponseValidator"/>, like a real one, so
    /// hidden questions stay unanswered and anything the rules reject is left out.
    /// </summary>
    public static class SampleResponseGenerator
    {
        /// <summary>How many days back the responses go.</summary>
        public const int Days = 21;

        private static readonly string[] FirstNames =
            ["Ada", "Alan", "Grace", "Katherine", "Linus", "Margaret", "Tim", "Barbara", "Dennis", "Radia", "Ken", "Frances", "Edsger", "Hedy", "John", "Mary"];

        private static readonly string[] LastNames =
            ["Lovelace", "Turing", "Hopper", "Johnson", "Torvalds", "Hamilton", "Berners-Lee", "Liskov", "Ritchie", "Perlman", "Thompson", "Allen", "Dijkstra", "Lamarr", "Backus", "Jackson"];

        private static readonly string[] Comments =
        [
            "More study rooms, please.",
            "The labs are great, but the Wi-Fi drops in the library.",
            "I'd like more evening classes.",
            "Advising helped me pick my courses.",
            "Parking is hard to find before 9.",
            "Loved the hackathon this semester.",
            "More group project space would help.",
            "The online portal is slow at registration time.",
        ];

        public static List<SurveyResponse> Generate(SurveyDefinition survey, IReadOnlyList<QuestionDefinition> questions,
            ResponseValidator validator, int count, DateTime now, int seed = 2026)
        {
            var random = new Random(seed);
            var responses = new List<SurveyResponse>();
            for (var attempt = 0; responses.Count < count && attempt < count * 5; attempt++)
            {
                var answers = Answer(questions, random, now);
                var result = validator.Validate(questions, answers);
                if (!result.IsValid)
                {
                    continue;
                }
                responses.Add(new SurveyResponse
                {
                    Id = Guid.NewGuid(),
                    SurveyId = survey.Id,
                    SubmittedAt = SubmittedAt(random, now),
                    Answers = result.Answers
                });
            }
            return responses.OrderBy(r => r.SubmittedAt).ToList();
        }

        // More answers arrive in the last week, as they would after a survey is shared.
        private static DateTime SubmittedAt(Random random, DateTime now)
        {
            var daysAgo = random.NextDouble() < 0.5 ? random.Next(0, 7) : random.Next(0, Days);
            return now.AddDays(-daysAgo).AddMinutes(-random.Next(0, 12 * 60));
        }

        private static Dictionary<string, List<string>> Answer(IReadOnlyList<QuestionDefinition> questions, Random random, DateTime now)
        {
            var student = random.NextDouble() < 0.7;
            var first = Pick(random, FirstNames);
            var last = Pick(random, LastNames);
            var answers = new Dictionary<string, List<string>>();

            foreach (var question in questions)
            {
                var value = question.Type.ToLowerInvariant() switch
                {
                    QuestionTypes.YesNo => [question.Key == "is_student" ? Bool(student) : Bool(random.NextDouble() < 0.5)],
                    QuestionTypes.Checkbox when question.Options.Count == 0 => [Bool(random.NextDouble() < 0.4)],
                    QuestionTypes.Number => [Number(question.Key, student, random)],
                    QuestionTypes.Rating => [random.Next(student ? 3 : 2, QuestionTypes.RatingScale(question) + 1).ToString(CultureInfo.InvariantCulture)],
                    QuestionTypes.Nps => [Nps(student, random)],
                    QuestionTypes.Slider => [Slider(question, student, random)],
                    QuestionTypes.Likert => Likert(question, student, random),
                    QuestionTypes.Email => [$"{first}.{last}@example.com".ToLowerInvariant()],
                    QuestionTypes.Date => [DateOnly.FromDateTime(now).AddDays(-random.Next(30, 3 * 365)).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)],
                    QuestionTypes.Multiselect or QuestionTypes.Checkbox => question.Options
                        .Where(_ => random.NextDouble() < 0.45).Select(o => o.Value).DefaultIfEmpty(Pick(random, question.Options).Value).ToList(),
                    var type when QuestionTypes.IsChoice(type) => [Choice(question, student, random)],
                    QuestionTypes.LongText => random.NextDouble() < 0.4 ? [Pick(random, Comments)] : [],
                    _ => question.Key switch
                    {
                        "first_name" => [first],
                        "last_name" => [last],
                        _ => question.Required ? ["Sample answer"] : new List<string>(),
                    },
                };
                if (value.Count > 0)
                {
                    answers[question.Key] = value;
                }
            }
            return answers;
        }

        private static string Number(string key, bool student, Random random) => key == "age"
            ? (student ? random.Next(18, 30) : random.Next(24, 65)).ToString(CultureInfo.InvariantCulture)
            : random.Next(1, 11).ToString(CultureInfo.InvariantCulture);

        // Students lean toward undergraduate and master's study.
        private static string Choice(QuestionDefinition question, bool student, Random random)
        {
            if (question.Key == "study_level" && student && random.NextDouble() < 0.7)
            {
                return random.NextDouble() < 0.6 ? "bachelor" : "master";
            }
            return Pick(random, question.Options).Value;
        }

        // Students are a little keener to recommend the place than everyone else.
        private static string Nps(bool student, Random random) =>
            Math.Min(QuestionTypes.NpsMax, random.Next(student ? 6 : 3, QuestionTypes.NpsMax + 1) + random.Next(0, 2))
                .ToString(CultureInfo.InvariantCulture);

        // Students spend more of the range (hours of study, say) than others.
        private static string Slider(QuestionDefinition question, bool student, Random random)
        {
            var (min, max) = QuestionTypes.SliderRange(question);
            var low = (int)min;
            var high = (int)max;
            var middle = low + (high - low) / 2;
            var value = student ? random.Next(middle - (high - low) / 4, high + 1) : random.Next(low, middle + 1);
            return Math.Clamp(value, low, high).ToString(CultureInfo.InvariantCulture);
        }

        // Each statement gets a rating that leans toward the agreeable end of the scale.
        private static List<string> Likert(QuestionDefinition question, bool student, Random random)
        {
            var scale = QuestionTypes.LikertScale(question);
            return question.Rows
                .Where(_ => question.Required || random.NextDouble() < 0.9)
                .Select(row => QuestionTypes.LikertAnswer(row.Value,
                    scale[Math.Min(scale.Count - 1, random.Next(scale.Count) + (student && random.NextDouble() < 0.4 ? 1 : 0))].Value))
                .ToList();
        }

        private static string Bool(bool value) => value ? "true" : "false";

        private static T Pick<T>(Random random, IReadOnlyList<T> items) => items[random.Next(items.Count)];
    }
}
