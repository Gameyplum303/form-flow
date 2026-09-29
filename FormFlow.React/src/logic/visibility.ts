import { QuestionDefinition } from "../types/QuestionDefinition";
import { Answers } from "../types/Survey";

/** Reads a yes/no style answer: "true"/"false" or "yes"/"no" in any case. */
export function parseBool(raw: string | undefined): boolean | undefined {
    switch (raw?.trim().toLowerCase()) {
        case "true":
        case "yes":
            return true;
        case "false":
        case "no":
            return false;
        default:
            return undefined;
    }
}

/**
 * Returns the keys of the questions that are visible for the given answers.
 * Mirrors VisibilityEvaluator in FormFlow.Data so the client hides exactly what the API ignores:
 * a question is hidden when the question it depends on is hidden, unanswered, missing, or
 * answered with a different value, and rules that form a cycle hide every question in it.
 */
export function visibleKeys(questions: QuestionDefinition[], answers: Answers): Set<string> {
    const byKey = new Map<string, QuestionDefinition>();
    questions.forEach((q) => {
        if (!byKey.has(q.key)) {
            byKey.set(q.key, q);
        }
    });

    const isVisible = (key: string, visiting: Set<string>): boolean => {
        const rule = byKey.get(key)?.visibleIf;
        if (!rule || !rule.key) {
            return true;
        }
        if (!byKey.has(rule.key) || visiting.has(key)) {
            return false;
        }
        visiting.add(key);
        if (!isVisible(rule.key, visiting)) {
            return false;
        }
        return parseBool(answers[rule.key]?.[0]) === rule.shouldEqual;
    };

    const visible = new Set<string>();
    byKey.forEach((_, key) => {
        if (isVisible(key, new Set<string>())) {
            visible.add(key);
        }
    });
    return visible;
}
