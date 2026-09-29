import { QuestionDefinition } from "./types/QuestionDefinition";
import { AnswerErrors, Answers, SurveyDefinition } from "./types/Survey";

/** Base URL of FormFlow.Backend. Override with REACT_APP_API_URL at build time. */
export const API_BASE = (process.env.REACT_APP_API_URL ?? "http://localhost:5164").replace(/\/$/, "");

async function getJson<T>(path: string): Promise<T> {
    const response = await fetch(`${API_BASE}${path}`, { headers: { Accept: "application/json" } });
    if (!response.ok) {
        throw new Error(`Request to ${path} failed with ${response.status}`);
    }
    return response.json() as Promise<T>;
}

export function getSurveys(): Promise<SurveyDefinition[]> {
    return getJson<SurveyDefinition[]>("/api/surveys");
}

export function getSurvey(id: string): Promise<SurveyDefinition> {
    return getJson<SurveyDefinition>(`/api/surveys/${id}`);
}

/** Opens a survey by the code in its share link. */
export function getSurveyByShareCode(code: string): Promise<SurveyDefinition> {
    return getJson<SurveyDefinition>(`/api/share/${encodeURIComponent(code)}`);
}

export function getSurveyQuestions(id: string): Promise<QuestionDefinition[]> {
    return getJson<QuestionDefinition[]>(`/api/surveys/${id}/questions`);
}

const RESPONDENT_KEY = "formflow.respondent";

function newRespondentId(): string {
    if (typeof crypto !== "undefined" && typeof crypto.randomUUID === "function") {
        return crypto.randomUUID();
    }
    return Array.from({ length: 32 }, () => Math.floor(Math.random() * 16).toString(16)).join("");
}

/**
 * The temporary id this browser answers surveys under. Respondents have no account, so the id
 * is what lets the API accept one answer per browser for each survey.
 */
export function respondentId(): string {
    try {
        const saved = localStorage.getItem(RESPONDENT_KEY);
        if (saved) {
            return saved;
        }
        const id = newRespondentId();
        localStorage.setItem(RESPONDENT_KEY, id);
        return id;
    } catch {
        // Storage can be blocked (private windows); the answer is still accepted without it.
        return newRespondentId();
    }
}

/** Whether this browser already answered the survey. Assumes not when the check fails. */
export async function hasAnswered(surveyId: string, respondent: string): Promise<boolean> {
    try {
        const result = await getJson<{ answered: boolean }>(
            `/api/surveys/${surveyId}/answered?respondentId=${encodeURIComponent(respondent)}`);
        return result.answered === true;
    } catch {
        return false;
    }
}

/**
 * The outcome of a submission. `final` means trying again won't help: the survey closed, was
 * taken offline, or this browser already answered it.
 */
export type SubmitResult =
    | { ok: true }
    | { ok: false; errors: AnswerErrors; message: string; final?: boolean };

/** Submits answers; validation problems come back keyed by question. */
export async function submitResponse(surveyId: string, answers: Answers, respondent?: string): Promise<SubmitResult> {
    const response = await fetch(`${API_BASE}/api/surveys/${surveyId}/responses`, {
        method: "POST",
        headers: { "Content-Type": "application/json", Accept: "application/json" },
        body: JSON.stringify({ answers, respondentId: respondent }),
    });

    if (response.ok) {
        return { ok: true };
    }

    if (response.status === 400) {
        const problem = (await response.json()) as { errors?: AnswerErrors };
        return { ok: false, errors: problem.errors ?? {}, message: "Please fix the highlighted answers." };
    }

    if (response.status === 404) {
        return { ok: false, errors: {}, message: "This survey is no longer available.", final: true };
    }

    if (response.status === 409) {
        const problem = (await response.json().catch(() => ({}))) as { error?: string };
        return { ok: false, errors: {}, message: problem.error ?? "This survey no longer takes answers.", final: true };
    }

    return { ok: false, errors: {}, message: `Your answers could not be submitted (${response.status}).` };
}
