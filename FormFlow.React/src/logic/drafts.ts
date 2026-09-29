import { Answers } from "../types/Survey";

/**
 * Answers someone has given but not yet submitted, kept in this browser per survey so they can
 * close the tab and pick up where they left off. Storage can be blocked (private windows, full
 * storage), so every call is best effort: answering still works, it just isn't remembered.
 */
const PREFIX = "formflow.answers.";

function isAnswers(value: unknown): value is Answers {
    return typeof value === "object" && value !== null && !Array.isArray(value)
        && Object.values(value).every((v) => Array.isArray(v) && v.every((s) => typeof s === "string"));
}

/** The saved answers for the survey, or null when there are none or they can't be read. */
export function loadDraft(surveyId: string): Answers | null {
    try {
        const raw = localStorage.getItem(PREFIX + surveyId);
        if (!raw) {
            return null;
        }
        const parsed: unknown = JSON.parse(raw);
        return isAnswers(parsed) && Object.keys(parsed).length > 0 ? parsed : null;
    } catch {
        return null;
    }
}

export function saveDraft(surveyId: string, answers: Answers): void {
    try {
        localStorage.setItem(PREFIX + surveyId, JSON.stringify(answers));
    } catch {
        // Storage is full or blocked.
    }
}

export function clearDraft(surveyId: string): void {
    try {
        localStorage.removeItem(PREFIX + surveyId);
    } catch {
        // Storage is blocked; nothing was saved.
    }
}
