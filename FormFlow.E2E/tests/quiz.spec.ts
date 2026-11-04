import { Browser, expect, Page, test } from "@playwright/test";
import { answer, choose, headersFor, openBlazor, professor, publish, question, runId, signIn, urls } from "./helpers";

const questionKey = `quiz_capital_${runId}`;
const questionLabel = `What is the capital of France? (${runId})`;
const quizTitle = `Geography quiz ${runId}`;
const explanation = "Paris has been the capital since the tenth century.";

/** A page in a fresh browser, as a student without an account (so with no respondent id yet). */
async function studentPage(browser: Browser): Promise<Page> {
    const context = await browser.newContext();
    return context.newPage();
}

test.describe.serial("Quizzes", () => {
    let page: Page;
    /** The share code of the quiz made from the Quiz template. */
    let templateQuiz: { id: string; shareCode: string };

    test.beforeAll(async ({ browser }) => {
        page = await (await browser.newContext()).newPage();
        await signIn(page, professor);
    });

    test.afterAll(async () => {
        await page.context().close();
    });

    test("a professor sets an answer key and makes a quiz in Blazor, and the key stays off the public API", async ({ request }) => {
        await openBlazor(page, "/admin/questions/create");
        await page.getByLabel("Label", { exact: true }).first().fill(questionLabel);
        await page.getByLabel("Key", { exact: true }).fill(questionKey);
        await choose(page, page.locator(".mud-select", { has: page.locator("label", { hasText: /^Type/ }) }), "Radio");
        for (const [optionLabel, value] of [["Paris", "paris"], ["Lyon", "lyon"]]) {
            await page.getByRole("button", { name: "Add Option" }).click();
            await page.locator("[data-option-row]").last().getByLabel("Label").fill(optionLabel);
            await page.locator("[data-option-row]").last().getByLabel("Value").fill(value);
            await page.locator("[data-option-row]").last().getByLabel("Value").press("Tab");
        }

        const answerKey = page.locator("[data-answer-key]");
        await expect(answerKey.getByText("Answer key (for quizzes)")).toBeVisible();
        await choose(page, answerKey.locator(".mud-select"), "Paris");
        await answerKey.getByLabel("Points").fill("2");
        await answerKey.getByLabel("Points").press("Tab");
        await answerKey.getByLabel("Explanation").fill(explanation);
        await answerKey.getByLabel("Explanation").press("Tab");
        await page.getByRole("button", { name: "Create Question" }).click();
        await expect(page.getByText(`Question '${questionLabel}' created successfully.`)).toBeVisible();

        const headers = await headersFor(request, professor);
        const bank: { key: string; id: string; answerKey: unknown }[] = await (await request.get(`${urls.api}/api/questions`, { headers })).json();
        const saved = bank.find(q => q.key === questionKey)!;
        expect(saved.answerKey).toEqual({ correctAnswers: ["paris"], points: 2, explanation });
        const publicCopy = await (await request.get(`${urls.api}/api/questions/${saved.id}`)).text();
        expect(publicCopy).not.toContain("correctAnswers");
        expect(publicCopy).not.toContain(explanation);

        // The survey editor turns it into a quiz that shows the correct answers.
        await openBlazor(page, "/admin/surveys/create");
        await page.getByRole("button", { name: "Start blank" }).click();
        await page.getByLabel("Survey Title").fill(quizTitle);
        await page.getByLabel("Description").fill("One question, no notes.");
        await page.locator(".mud-list-item", { hasText: questionLabel }).getByRole("button", { name: "Add" }).click();
        await page.getByLabel("Grade answers as a quiz").check();
        await expect(page.locator("[data-quiz-graded]")).toContainText("1 of 1 question is graded, worth 2 points in all.");
        await page.locator("[data-quiz-feedback] label.mud-radio", { hasText: "the correct answers" }).click();
        await page.getByRole("button", { name: "Save Survey" }).click();
        await expect(page).toHaveURL(/\/admin\/surveys$/);

        const managed: { id: string; title: string; isQuiz: boolean; quizFeedback: string }[] =
            await (await request.get(`${urls.api}/api/surveys/managed`, { headers })).json();
        expect(managed.find(s => s.title === quizTitle)).toMatchObject({ isQuiz: true, quizFeedback: "answers" });
    });

    test("a student takes the Quiz template in Blazor and sees the score and each answer", async ({ browser, request }) => {
        const headers = await headersFor(request, professor);
        const templates: { id: string; title: string; isQuiz: boolean }[] = await (await request.get(`${urls.api}/api/templates`, { headers })).json();
        const template = templates.find(t => t.title === "Quiz")!;
        expect(template.isQuiz).toBe(true);
        const created = await request.post(`${urls.api}/api/templates/${template.id}/use`, { headers });
        expect(created.status()).toBe(201);
        templateQuiz = await created.json();
        await publish(request, templateQuiz.id, headers);

        // Respondents load the questions without the answer key.
        const form = await (await request.get(`${urls.api}/api/surveys/${templateQuiz.id}/questions`)).text();
        expect(form).not.toContain("correctAnswers");

        const student = await studentPage(browser);
        await openBlazor(student, `/s/${templateQuiz.shareCode}`);
        await expect(student.locator("[data-quiz-notice]")).toContainText("This is a quiz.");
        await question(student, "quiz_cpu").locator("label.mud-radio").first().click();
        await choose(student, question(student, "quiz_byte").locator(".mud-select"), "16");
        await answer(student, "quiz_binary", "10");
        await question(student, "quiz_ram_volatile").locator("label.mud-radio").first().click();
        await question(student, "quiz_languages").locator("input[type=checkbox]").nth(0).check();
        await question(student, "quiz_languages").locator("input[type=checkbox]").nth(1).check();
        await answer(student, "quiz_html", "hypertext");
        await student.getByRole("button", { name: "Submit" }).click();

        // Two of three languages earn two thirds of 2 points; the byte question is wrong.
        await expect(student.getByText("Thank you!")).toBeVisible();
        await expect(student.locator("[data-quiz-score]")).toHaveText("5.33 of 7 points");
        await expect(student.locator("[data-quiz-percent]")).toHaveText("76%");
        await expect(student.locator("[data-quiz-question]")).toHaveCount(6);
        const byte = student.locator("[data-quiz-question=quiz_byte]");
        await expect(byte).toHaveAttribute("data-correct", "false");
        await expect(byte.locator("[data-quiz-verdict]")).toHaveText("Incorrect");
        await expect(byte.locator("[data-quiz-correct-answer]")).toContainText("8");
        await expect(byte.locator("[data-quiz-explanation]")).toContainText("A byte is 8 bits");
        await expect(student.locator("[data-quiz-question=quiz_languages] [data-quiz-verdict]")).toHaveText("Partly correct");
        await expect(student.locator("[data-quiz-question=quiz_cpu] [data-quiz-verdict]")).toHaveText("Correct");
        await student.context().close();
    });

    test("a student takes the same quiz in React and sees the score and each answer", async ({ browser }) => {
        const student = await studentPage(browser);
        await student.goto(`${urls.react}/#/s/${templateQuiz.shareCode}`);
        await expect(student.getByText(/This is a quiz/)).toBeVisible();
        await student.getByRole("group", { name: /carries out/ }).getByLabel("The processor (CPU)").check();
        await student.getByLabel(/bits are in a byte/).selectOption("8");
        await student.getByLabel(/binary number 1010/).fill("10");
        await student.getByRole("group", { name: /switched off/ }).getByLabel("Yes").check();
        const languages = student.getByRole("group", { name: /programming languages/ });
        for (const language of ["Python", "Java", "C#"]) {
            await languages.getByLabel(language, { exact: true }).check();
        }
        await student.getByLabel(/stand for/).fill("Hyper Text");
        await student.getByRole("button", { name: "Submit" }).click();

        await expect(student.getByText("Thank you!")).toBeVisible();
        await expect(student.locator("[data-quiz-score]")).toHaveText("7 of 7 points");
        await expect(student.locator("[data-quiz-percent]")).toHaveText("100%");
        await expect(student.locator("[data-quiz-question][data-correct=true]")).toHaveCount(6);
        await student.context().close();
    });

    test("the professor sees the quiz summary on the results page, and scores in the CSV", async ({ request }) => {
        await openBlazor(page, `/admin/surveys/${templateQuiz.id}/results`);
        const summary = page.locator("[data-quiz-summary]");
        await expect(summary).toBeVisible();
        await expect(summary).toContainText("2 graded responses");
        await expect(summary.locator("[data-quiz-average]")).toHaveText("6.17 of 7 points");
        await expect(summary.locator("[data-quiz-average-percent]")).toHaveText("88.1%");
        await expect(summary.locator("[data-score-band='70–79%']")).toHaveAttribute("data-count", "1");
        await expect(summary.locator("[data-score-band='90–100%']")).toHaveAttribute("data-count", "1");
        await expect(summary.locator("[data-quiz-question=quiz_byte]")).toContainText("1 of 2 (50%)");
        await expect(summary.locator("[data-quiz-question=quiz_cpu]")).toContainText("2 of 2 (100%)");

        // Filters narrow the summary too.
        await page.locator("[data-add-filter]").selectOption("quiz_byte:16");
        await expect(summary).toContainText("1 graded response");
        await expect(summary.locator("[data-quiz-average]")).toHaveText("5.33 of 7 points");

        const headers = await headersFor(request, professor);
        const csv = await (await request.get(`${urls.api}/api/surveys/${templateQuiz.id}/responses/export`, { headers })).text();
        const [header, ...rows] = csv.trim().split("\r\n");
        expect(header.startsWith("response_id,submitted_at,submitted_by,score,max_score,quiz_cpu")).toBe(true);
        expect(rows.map(r => r.split(",").slice(3, 5).join("/")).sort()).toEqual(["5.33/7", "7/7"]);
    });
});
