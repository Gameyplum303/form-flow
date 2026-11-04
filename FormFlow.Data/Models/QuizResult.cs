namespace FormFlow.Data.Models
{
    /// <summary>
    /// A respondent's grade on a quiz, returned when they submit. <see cref="Questions"/> is only filled
    /// in when the survey shows the correct answers (<see cref="QuizFeedbackModes.Answers"/>).
    /// </summary>
    public class QuizResult
    {
        /// <summary>The points earned, rounded to two decimal places.</summary>
        public decimal Score { get; set; }

        /// <summary>The points available: the graded questions the respondent saw. Hidden questions don't count.</summary>
        public decimal MaxScore { get; set; }

        /// <summary>The score as a whole-number percentage of <see cref="MaxScore"/>, or 0 when nothing was graded.</summary>
        public int Percent { get; set; }

        /// <summary>One entry per graded question, in survey order, when the survey shows the correct answers.</summary>
        public List<QuestionGrade> Questions { get; set; } = [];
    }

    /// <summary>How one question of a quiz was graded.</summary>
    public class QuestionGrade
    {
        public string Key { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;

        /// <summary>The points earned. Checkbox and multiselect questions can earn part of their points.</summary>
        public decimal Points { get; set; }

        /// <summary>What the question is worth.</summary>
        public decimal MaxPoints { get; set; }

        /// <summary>Whether the answer was exactly right.</summary>
        public bool Correct { get; set; }

        /// <summary>The respondent's answer as they saw it (option labels, "Yes" or "No"); empty when unanswered.</summary>
        public List<string> GivenAnswers { get; set; } = [];

        /// <summary>The correct answer as respondents see it. For text, every accepted answer.</summary>
        public List<string> CorrectAnswers { get; set; } = [];

        public string? Explanation { get; set; }
    }
}
