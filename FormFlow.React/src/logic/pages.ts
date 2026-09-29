import { QuestionDefinition } from "../types/QuestionDefinition";
import { AnswerErrors, Answers } from "../types/Survey";

/** The message the API gives an unanswered required question, so both read the same. */
export const REQUIRED_MESSAGE = "This question is required.";

/**
 * The survey's questions, page by page: a new page starts at each question whose id is in
 * `pageBreaks`. A survey without page breaks is one page. Mirrors SurveyPaging.Split in FormFlow.Data.
 */
export function splitPages(questions: QuestionDefinition[], pageBreaks: string[] | undefined): QuestionDefinition[][] {
    const breaks = new Set(pageBreaks ?? []);
    const pages: QuestionDefinition[][] = [[]];
    questions.forEach((q) => {
        if (pages[pages.length - 1].length > 0 && breaks.has(q.id)) {
            pages.push([]);
        }
        pages[pages.length - 1].push(q);
    });
    return pages;
}

/**
 * The indexes of the pages a respondent sees: those with at least one visible question.
 * A page whose questions are all hidden is skipped. Always at least one page.
 */
export function shownPages(pages: QuestionDefinition[][], visible: Set<string>): number[] {
    const shown = pages.map((_, i) => i).filter((i) => pages[i].some((q) => visible.has(q.key)));
    return shown.length > 0 ? shown : [0];
}

/** Required questions on the page that are visible but unanswered, with the API's message. */
export function missingAnswers(page: QuestionDefinition[], visible: Set<string>, answers: Answers): AnswerErrors {
    const errors: AnswerErrors = {};
    page
        .filter((q) => q.required && visible.has(q.key))
        .filter((q) => !(answers[q.key] ?? []).some((v) => v.trim() !== ""))
        .forEach((q) => {
            errors[q.key] = [REQUIRED_MESSAGE];
        });
    return errors;
}

/** The index of the first page with an error, or undefined when none of the errors belong to a page. */
export function firstPageWithError(pages: QuestionDefinition[][], errors: AnswerErrors): number | undefined {
    const index = pages.findIndex((page) => page.some((q) => (errors[q.key]?.length ?? 0) > 0));
    return index >= 0 ? index : undefined;
}
