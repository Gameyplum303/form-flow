namespace FormFlow.Data.Models
{
    /// <summary>
    /// How a question is graded when it is part of a quiz: the correct answer or answers, the points it
    /// is worth, and an explanation shown after submitting. Only builders who manage the question, or a
    /// survey that uses it, see it; everyone else gets <c>null</c>. See <see cref="Services.QuizScorer"/>.
    /// </summary>
    public class AnswerKey
    {
        /// <summary>
        /// The correct answers, as stored answer values: one option value for radio and dropdown, "true"
        /// or "false" for yes/no, the exact set of option values for checkbox and multiselect, one number
        /// for number, and one or more accepted answers for text.
        /// </summary>
        public List<string> CorrectAnswers { get; set; } = [];

        /// <summary>What a correct answer is worth: zero or more, decimals allowed.</summary>
        public decimal Points { get; set; } = DefaultPoints;

        /// <summary>Why the answer is correct, shown after submitting when the survey shows the correct answers.</summary>
        public string? Explanation { get; set; }

        public const decimal DefaultPoints = 1;

        /// <summary>The most points one question can be worth.</summary>
        public const decimal MaxPoints = 1000;

        /// <summary>The longest explanation, in characters.</summary>
        public const int MaxExplanationLength = 2000;
    }
}
