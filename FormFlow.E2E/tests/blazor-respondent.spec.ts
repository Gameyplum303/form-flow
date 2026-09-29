import { expect, test } from "@playwright/test";
import { adminHeaders, answer, choose, demoSurveyTitle, openBlazor, question, signIn, surveyByTitle, urls } from "./helpers";

test.describe("Blazor: taking a survey", () => {
    test("shows the survey list from the home page", async ({ page }) => {
        await openBlazor(page, "/");
        await page.getByRole("link", { name: "Take a Survey" }).first().click();
        await expect(page).toHaveURL(/\/surveys$/);
        await expect(page.getByText(demoSurveyTitle)).toBeVisible();
    });

    test("validates on the server, then submits and updates the results", async ({ page, request }) => {
        const survey = await surveyByTitle(request, demoSurveyTitle);
        const before = await (await request.get(`${urls.api}/api/surveys/${survey.id}/results`, { headers: await adminHeaders(request) })).json();

        await openBlazor(page, `/surveys/${survey.id}`);
        const isStudent = question(page, "is_student").locator("label.mud-radio");

        // The campus question depends on "Are you currently a student?"
        await expect(question(page, "campus_preference")).toHaveCount(0);
        await isStudent.first().click();
        await expect(question(page, "campus_preference")).toHaveCount(1);
        await isStudent.nth(1).click();
        await expect(question(page, "campus_preference")).toHaveCount(0);

        await page.getByRole("button", { name: "Submit" }).click();
        await expect(page.getByText("Please fix the highlighted answers.")).toBeVisible();
        await expect(page.getByText("This question is required.")).toHaveCount(7);

        await isStudent.first().click();
        await answer(page, "first_name", "Ada");
        await answer(page, "last_name", "Lovelace");
        await answer(page, "email", "ada@example.com");
        await answer(page, "age", "130");
        await choose(page, question(page, "study_level").locator(".mud-select"), "Master");
        await question(page, "contact_method").locator("label.mud-radio").nth(1).click();
        await question(page, "skills").locator("input[type=checkbox]").first().check();
        await choose(page, question(page, "campus_preference").locator(".mud-select"), "East");

        await page.getByRole("button", { name: "Submit" }).click();
        await expect(question(page, "age").getByText("Value must be ≤ 120.")).toBeVisible();
        await expect(page.getByText("This question is required.")).toHaveCount(0);

        await answer(page, "age", "36");
        await page.getByRole("button", { name: "Submit" }).click();
        await expect(page.getByText("Thank you!")).toBeVisible();

        await page.getByRole("button", { name: "Submit another response" }).click();
        await expect(question(page, "first_name").locator("input")).toHaveValue("");

        const after = await (await request.get(`${urls.api}/api/surveys/${survey.id}/results`, { headers: await adminHeaders(request) })).json();
        expect(after.totalResponses).toBe(before.totalResponses + 1);
    });

    test("results page shows statistics and downloads CSV", async ({ page, request }) => {
        const survey = await surveyByTitle(request, demoSurveyTitle);
        const submitted = await request.post(`${urls.api}/api/surveys/${survey.id}/responses`, {
            data: {
                answers: {
                    first_name: "Alan", last_name: "Turing", email: "alan@example.com", age: 41, is_student: false,
                    study_level: "phd", contact_method: "email", skills: ["csharp", "sql"],
                },
            },
        });
        expect(submitted.status()).toBe(201);

        await signIn(page);
        await openBlazor(page, `/admin/surveys/${survey.id}/results`);
        await expect(page.getByText(/^\d+ responses?, latest/)).toBeVisible();
        await expect(page.getByText("Average")).toBeVisible();

        const [download] = await Promise.all([
            page.waitForEvent("download"),
            page.getByText("Download CSV").click(),
        ]);
        expect(download.suggestedFilename()).toBe("student-experience-survey-responses.csv");
        const csv = await download.createReadStream().then(async stream => {
            let text = "";
            for await (const chunk of stream) text += chunk;
            return text;
        });
        expect(csv.split("\r\n")[0]).toBe(
            "response_id,submitted_at,first_name,last_name,email,age,is_student,study_level,contact_method,subscribe_newsletter,skills,campus_preference");
        expect(csv).toContain("Turing");
        expect(csv).toContain("csharp; sql");
    });

    test("shows a message for a survey that does not exist", async ({ page }) => {
        await openBlazor(page, "/surveys/00000000-0000-0000-0000-000000000001");
        await expect(page.getByText("This survey does not exist or is no longer available.")).toBeVisible();
    });
});
