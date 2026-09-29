export interface SurveyDefinition {
    id: string;
    title: string;
    description: string;
    questionIds: string[];
    createdAt: string;
}

/** Answers keyed by question key; multi-select questions hold several values. */
export type Answers = Record<string, string[]>;

/** Validation messages keyed by question key, as returned by the API. */
export type AnswerErrors = Record<string, string[]>;
