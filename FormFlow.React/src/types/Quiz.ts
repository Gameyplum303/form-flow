/** How one question of a quiz was graded. Answers are written as the respondent saw them (option labels, Yes or No). */
export interface QuestionGrade {
    key: string;
    label: string;
    /** The points earned; checkbox and multiselect questions can earn part of their points. */
    points: number;
    maxPoints: number;
    /** Whether the answer was exactly right. */
    correct: boolean;
    givenAnswers: string[];
    correctAnswers: string[];
    explanation?: string | null;
}

/** A respondent's grade, returned by the API when they submit a quiz. */
export interface QuizResult {
    score: number;
    /** The points available: graded questions the respondent saw. */
    maxScore: number;
    /** The score as a whole-number percentage, or 0 when nothing was graded. */
    percent: number;
    /** Filled in only when the survey shows the correct answers. */
    questions: QuestionGrade[];
}
