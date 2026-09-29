export interface SurveyDefinition {
    id: string;
    title: string;
    description: string;
    questionIds: string[];
    createdAt: string;
    /** "draft" or "published"; only the owner can open a draft. */
    status?: string;
    /** Whether a published survey shows on the public list or is reachable by its link only. */
    listed?: boolean;
    /** The code in the survey's share link, #/s/<code>. */
    shareCode?: string;
    /** When the survey stops taking answers (UTC), if the owner set a close date. */
    closesAt?: string | null;
}

/** Answers keyed by question key; multi-select questions hold several values. */
export type Answers = Record<string, string[]>;

/** Validation messages keyed by question key, as returned by the API. */
export type AnswerErrors = Record<string, string[]>;
