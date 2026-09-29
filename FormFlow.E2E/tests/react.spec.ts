import { expect, test } from "@playwright/test";
import { adminHeaders, demoSurveyTitle, surveyByTitle, urls } from "./helpers";

test.describe("React: taking a survey", () => {
    test("validates on the server and stores the answers", async ({ page, request }) => {
        await page.goto(urls.react);
        await expect(page.getByText(demoSurveyTitle)).toBeVisible();
        await page.getByRole("button", { name: "Take survey" }).first().click();
        await expect(page).toHaveURL(/#\/surveys\//);

        await expect(page.getByText("Preferred campus location")).toHaveCount(0);
        await page.getByRole("button", { name: "Submit" }).click();
        await expect(page.getByText("This question is required.")).toHaveCount(8);

        await page.getByRole("group", { name: /currently a student/ }).getByLabel("Yes").check();
        await expect(page.getByText("Preferred campus location")).toBeVisible();

        const lastName = `Hopper-${Date.now()}`;
        await page.getByLabel(/First Name/).fill("Grace");
        await page.getByLabel(/Last Name/).fill(lastName);
        await page.getByLabel(/Email Address/).fill("grace@example.com");
        await page.getByLabel(/How old are you/).fill("-5");
        await page.getByLabel(/highest level of study/).selectOption("phd");
        await page.getByRole("group", { name: /contact method/ }).getByLabel("Email").check();
        await page.getByRole("group", { name: /skills/ }).getByLabel("SQL").check();
        await page.getByLabel(/campus location/).selectOption("west");
        await page.getByLabel(/start your current program/).fill("2024-01-15");
        await page.getByRole("group", { name: /rate your experience/ }).getByLabel("3 of 5").check();
        await page.getByLabel(/Anything else/).fill("More study rooms, please.");

        await page.getByRole("button", { name: "Submit" }).click();
        await expect(page.getByText("Value must be ≥ 0.")).toBeVisible();

        await page.getByLabel(/How old are you/).fill("40");
        await page.getByRole("button", { name: "Submit" }).click();
        await expect(page.getByText(/Thank you/)).toBeVisible();

        const survey = await surveyByTitle(request, demoSurveyTitle);
        const responses = await (await request.get(`${urls.api}/api/surveys/${survey.id}/responses`, { headers: await adminHeaders(request) })).json();
        const saved = responses.find((r: any) => r.answers.last_name?.[0] === lastName);
        expect(saved.answers).toMatchObject({
            is_student: ["true"],
            age: ["40"],
            campus_preference: ["west"],
            skills: ["sql"],
            program_start: ["2024-01-15"],
            experience_rating: ["3"],
            comments: ["More study rooms, please."],
        });
    });

    test("shows a message when the survey does not exist", async ({ page }) => {
        await page.goto(`${urls.react}/#/surveys/00000000-0000-0000-0000-000000000001`);
        await expect(page.getByText(/not found|does not exist|could not/i)).toBeVisible();
    });
});
