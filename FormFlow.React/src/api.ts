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

export function getSurveyQuestions(id: string): Promise<QuestionDefinition[]> {
    return getJson<QuestionDefinition[]>(`/api/surveys/${id}/questions`);
}

export type SubmitResult = { ok: true } | { ok: false; errors: AnswerErrors; message: string };

/** Submits answers; validation problems come back keyed by question. */
export async function submitResponse(surveyId: string, answers: Answers): Promise<SubmitResult> {
    const response = await fetch(`${API_BASE}/api/surveys/${surveyId}/responses`, {
        method: "POST",
        headers: { "Content-Type": "application/json", Accept: "application/json" },
        body: JSON.stringify({ answers }),
    });

    if (response.ok) {
        return { ok: true };
    }

    if (response.status === 400) {
        const problem = (await response.json()) as { errors?: AnswerErrors };
        return { ok: false, errors: problem.errors ?? {}, message: "Please fix the highlighted answers." };
    }

    return { ok: false, errors: {}, message: `Your answers could not be submitted (${response.status}).` };
}
