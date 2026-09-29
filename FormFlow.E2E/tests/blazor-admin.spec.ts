import { expect, Page, test } from "@playwright/test";
import { admin, answer, choose, demoSurveyTitle, openBlazor, question, runId, signIn, submitSignIn, surveyByTitle, viewer } from "./helpers";

const key = `campus_job_${runId}`;
const label = `Which campus job do you have? (${runId})`;
const editedLabel = `What campus job do you have? (${runId})`;
const surveyTitle = `Campus Life ${runId}`;

function questionRow(page: Page, text: string) {
    return page.locator("tr", { hasText: text });
}

async function confirmDelete(page: Page, name: string) {
    await page.getByRole("button", { name: `Delete ${name}` }).click();
    await page.locator(".mud-dialog").getByRole("button", { name: "Delete" }).click();
}

test.describe("Blazor: signing in", () => {
    test("sends a signed-out admin to sign in, then back to the page they asked for", async ({ page }) => {
        await openBlazor(page, "/admin/questions");
        await expect(page).toHaveURL(/\/login\?returnUrl=%2Fadmin%2Fquestions$/);

        await submitSignIn(page, admin.username, "not-the-password");
        await expect(page.getByText("Invalid username or password.")).toBeVisible();

        await submitSignIn(page, admin.username, admin.password);
        await expect(page).toHaveURL(/\/admin\/questions$/);
        await expect(page.getByText(`Signed in as ${admin.username}`)).toBeVisible();

        // The sign-in survives a reload, and signing out ends it.
        await openBlazor(page, "/admin/questions");
        await expect(page.getByRole("button", { name: "Sign out" })).toBeVisible();
        await page.getByRole("button", { name: "Sign out" }).click();
        await expect(page).toHaveURL(/\/login\?returnUrl=%2Fadmin%2Fquestions$/);
        await openBlazor(page, "/admin/surveys");
        await expect(page).toHaveURL(/\/login\?returnUrl=%2Fadmin%2Fsurveys$/);
    });
});

test.describe("Blazor: view-only account", () => {
    test("can see surveys and results but not change anything", async ({ page, request }) => {
        await signIn(page, viewer);
        await expect(page.getByText(`Signed in as ${viewer.username} (view only)`)).toBeVisible();
        await expect(page.getByText("You're signed in with a view-only account.")).toBeVisible();
        await expect(questionRow(page, demoSurveyTitle)).toHaveCount(1);
        await expect(page.getByText("+ Create Survey")).toHaveCount(0);
        await expect(page.getByRole("button", { name: `Delete ${demoSurveyTitle}` })).toHaveCount(0);

        await questionRow(page, demoSurveyTitle).getByRole("button", { name: "Results" }).click();
        await expect(page.getByText(/^\d+ responses?/)).toBeVisible();

        const survey = await surveyByTitle(request, demoSurveyTitle);
        await openBlazor(page, `/admin/surveys/${survey.id}/edit`);
        await expect(page.getByText("Only admins can create or edit questions and surveys.")).toBeVisible();
        await expect(page.getByRole("button", { name: "Save Survey" })).toHaveCount(0);
    });
});

// Each step builds on the previous one, so they run in order and stop at the first failure.
// They share one signed-in tab, like an admin working through the pages.
test.describe.serial("Blazor: admin", () => {
    let page: Page;

    test.beforeAll(async ({ browser }) => {
        page = await browser.newPage();
        await signIn(page);
    });

    test.afterAll(async () => {
        await page.close();
    });

    test("creates a conditional question with an answer rule", async () => {
        await openBlazor(page, "/admin/questions");
        await page.getByText("Create New Question").click();
        await expect(page).toHaveURL(/\/admin\/questions\/create$/);
        await expect(page.locator(".page[data-interactive=true]")).toBeVisible();

        await page.getByLabel("Label").first().fill(label);
        await page.getByLabel("Key").first().fill(key);
        await choose(page, page.locator(".mud-select", { has: page.locator("label", { hasText: /^Type/ }) }), "Text");
        await page.getByLabel("Minimum length").fill("3");
        await page.getByLabel("Minimum length").press("Tab");
        await choose(page, page.locator(".mud-select", { has: page.locator("label", { hasText: "Only show when" }) }),
            "Are you currently a student?");
        await page.getByRole("button", { name: "Create Question" }).click();
        await expect(page.getByText(`Question '${label}' created successfully.`)).toBeVisible();

        await openBlazor(page, "/admin/questions");
        await expect(questionRow(page, key)).toHaveCount(1);
        await expect(questionRow(page, key).getByText("is_student is yes")).toBeVisible();
    });

    test("edits the question", async () => {
        await openBlazor(page, "/admin/questions");
        await questionRow(page, key).getByRole("button", { name: "Edit" }).click();
        await expect(page).toHaveURL(/\/edit$/);
        await expect(page.getByLabel("Minimum length")).toHaveValue("3");

        const labelField = page.getByLabel("Label").first();
        await labelField.fill(editedLabel);
        await labelField.press("Tab");
        await page.getByRole("button", { name: "Save Changes" }).click();
        await expect(page).toHaveURL(/\/admin\/questions$/);
        await expect(page.locator("td", { hasText: editedLabel })).toHaveCount(1);
    });

    test("builds a survey and warns about question order", async () => {
        await openBlazor(page, "/admin/surveys");
        await page.getByText("+ Create Survey").click();
        await expect(page.locator(".page[data-interactive=true]")).toBeVisible();
        await page.getByLabel("Survey Title").fill(surveyTitle);
        await page.getByLabel("Description").fill("Short survey about campus life.");

        const add = (text: string) => page.locator(".mud-list-item", { hasText: text }).getByRole("button", { name: "Add" }).click();
        await add(editedLabel);
        await expect(page.getByText(/depends on "is_student", which is not in this survey/)).toBeVisible();
        await add("Are you currently a student?");
        await expect(page.getByText(/appears before the question it depends on/)).toBeVisible();
        await page.getByRole("button", { name: "Move down" }).first().click();
        await expect(page.getByText(/appears before the question it depends on/)).toHaveCount(0);

        await page.getByRole("button", { name: "Save Survey" }).click();
        await openBlazor(page, "/admin/surveys");
        await expect(questionRow(page, surveyTitle)).toHaveCount(1);
    });

    test("previews the survey with the conditional question", async () => {
        await openBlazor(page, "/admin/surveys");
        await questionRow(page, surveyTitle).getByRole("button", { name: "Preview" }).click();
        await expect(page.locator(".page[data-interactive=true]")).toBeVisible();
        await expect(question(page, "is_student")).toBeVisible();
        await expect(question(page, key)).toHaveCount(0);
        await question(page, "is_student").locator("label.mud-radio").first().click();
        await expect(question(page, key)).toHaveCount(1);
    });

    test("enforces the new question's rule when answering", async ({ request }) => {
        const survey = await surveyByTitle(request, surveyTitle);
        await openBlazor(page, `/surveys/${survey.id}`);
        await question(page, "is_student").locator("label.mud-radio").first().click();
        await answer(page, key, "TA");
        await page.getByRole("button", { name: "Submit" }).click();
        await expect(question(page, key).getByText("Minimum length is 3.")).toBeVisible();

        await answer(page, key, "Library assistant");
        await page.getByRole("button", { name: "Submit" }).click();
        await expect(page.getByText("Thank you!")).toBeVisible();
    });

    test("refuses to delete a question a survey uses", async () => {
        await openBlazor(page, "/admin/questions");
        await confirmDelete(page, editedLabel);
        await expect(page.getByText(`This question is used by: ${surveyTitle}.`, { exact: false })).toBeVisible();
        await expect(page.locator("td", { hasText: editedLabel })).toHaveCount(1);
    });

    test("deletes the survey, then the question", async () => {
        await openBlazor(page, "/admin/surveys");
        await confirmDelete(page, surveyTitle);
        await expect(questionRow(page, surveyTitle)).toHaveCount(0);

        await openBlazor(page, "/admin/questions");
        await page.getByRole("button", { name: `Delete ${editedLabel}` }).click();
        await page.locator(".mud-dialog").getByRole("button", { name: "Cancel" }).click();
        await expect(page.locator("td", { hasText: editedLabel })).toHaveCount(1);

        await confirmDelete(page, editedLabel);
        await expect(page.locator("td", { hasText: editedLabel })).toHaveCount(0);
    });

    test("shows not found for a missing survey preview", async () => {
        await openBlazor(page, "/admin/surveys/00000000-0000-0000-0000-000000000001/preview");
        await expect(page.getByText("Survey not found")).toBeVisible();
    });
});
