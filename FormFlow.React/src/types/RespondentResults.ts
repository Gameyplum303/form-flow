/**
 * The summary respondents see after answering a survey whose owner shares its results, from
 * GET /api/surveys/{id}/results/public. Only questions with a fixed set of answers are in it.
 */
export interface RespondentResults {
    surveyId: string;
    title: string;
    totalResponses: number;
    questions: RespondentQuestionResult[];
}

export interface RespondentQuestionResult {
    key: string;
    label: string;
    type: string;
    /** How many responses answered the question; the percentages are of these. */
    answeredCount: number;
    /** Whether people could pick more than one option, so the percentages can add up to more than 100. */
    multipleAnswers: boolean;
    options: RespondentOptionResult[];
}

export interface RespondentOptionResult {
    value: string;
    label: string;
    count: number;
    percent: number;
}
