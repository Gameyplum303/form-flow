import { expect, Page, test } from "@playwright/test";
import {
    admin, answer, approvedProfessor, choose, demoSurveyTitle, emailedLink, headersFor, newSignUp, openBlazor, professor, question, runId, signIn,
    submitSignIn, surveyByTitle, urls,
} from "./helpers";

const key = `campus_job_${runId}`;
const label = `Which campus job do you have? (${runId})`;
const editedLabel = `What campus job do you have? (${runId})`;
const surveyTitle = `Campus Life ${runId}`;
/** The survey's share link, read from its Share page. */
let shareLink = "";

function questionRow(page: Page, text: string) {
    return page.locator("tr", { hasText: text });
}

// The question bank shows 10 rows a page in storage order, so a new question can land on page 2. Show them all.
async function showAllQuestions(page: Page) {
    await choose(page, page.locator(".mud-table-pagination .mud-select"), "100");
}

async function openQuestionBank(page: Page) {
    await openBlazor(page, "/admin/questions");
    await showAllQuestions(page);
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

test.describe("Blazor: roles", () => {
    test("a professor signs up, verifies their email, waits for approval, then signs in", async ({ browser, request }) => {
        const signUp = newSignUp("Blazor Grace");
        const page = await browser.newPage();
        await openBlazor(page, "/login");
        await page.getByRole("link", { name: "Sign up" }).click();
        await expect(page).toHaveURL(/\/signup$/);
        await expect(page.locator(".page[data-interactive=true]")).toBeVisible();

        const fill = async (selector: string, value: string) => {
            await page.locator(selector).fill(value);
            await page.locator(selector).press("Tab");
        };
        await fill("input[autocomplete=name]", signUp.name);
        await fill("input[autocomplete=email]", signUp.email);
        await fill("input[type=password] >> nth=0", signUp.password);
        await fill("input[type=password] >> nth=1", "not the same");
        await fill("input[type=date]", signUp.dateOfBirth);
        await fill("input[autocomplete=organization]", signUp.organization);
        await fill("textarea", signUp.intendedUse);
        await page.getByRole("button", { name: "Sign up" }).click();
        await expect(page.locator("[data-field-error=confirmPassword]")).toHaveText("The passwords don't match.");

        await fill("input[type=password] >> nth=1", signUp.password);
        await page.getByRole("button", { name: "Sign up" }).click();
        await expect(page.locator("[data-signup-done]")).toContainText(`We sent a link to ${signUp.email}`);

        // Signing in before verifying offers a fresh link; only the newest one works.
        await openBlazor(page, "/login");
        await submitSignIn(page, signUp.email, signUp.password);
        await expect(page.getByText("Please verify your email address first.")).toBeVisible();
        await page.getByRole("button", { name: "Email me a new link" }).click();
        await expect(page.locator("[data-verification-sent]")).toContainText(`We sent a new verification link to ${signUp.email}.`);

        await openBlazor(page, await emailedLink(request, signUp.email, "verify-email"));
        await expect(page.locator("[data-email-verified]")).toContainText("your email address is verified");

        await openBlazor(page, "/login");
        await submitSignIn(page, signUp.email, signUp.password);
        await expect(page.getByText("Your account is waiting for an administrator's approval.")).toBeVisible();

        // An administrator approves the sign-up from the Sign-ups page.
        const adminPage = await browser.newPage();
        await signIn(adminPage);
        await adminPage.getByRole("link", { name: "Sign-ups" }).click();
        await expect(adminPage).toHaveURL(/\/admin\/signups$/);
        const row = adminPage.locator("tr", { hasText: signUp.email });
        await expect(row).toContainText(signUp.organization);
        await expect(row).toContainText(signUp.intendedUse);
        await expect(row.locator("[data-email-verified]")).toHaveText("Verified");
        await row.getByRole("button", { name: "Approve" }).click();
        await expect(adminPage.getByText(`Approved ${signUp.name}. They can sign in now.`)).toBeVisible();
        await expect(row).toHaveCount(0);
        await adminPage.close();

        await submitSignIn(page, signUp.email, signUp.password);
        await expect(page).toHaveURL(/\/admin\/surveys$/);
        await expect(page.getByText(`Signed in as ${signUp.email} (Professor/Scientist)`)).toBeVisible();
        await expect(page.getByRole("link", { name: "Sign-ups" })).toHaveCount(0);
        await page.close();
    });

    test("a professor resets a forgotten password, then changes it", async ({ browser, request }) => {
        const ada = await approvedProfessor(request, "Blazor Reset");
        const page = await browser.newPage();
        await openBlazor(page, "/login");
        await page.getByRole("link", { name: "Forgot your password?" }).click();
        await expect(page).toHaveURL(/\/forgot-password$/);
        await page.locator("input[autocomplete=email]").fill(ada.email);
        await page.locator("input[autocomplete=email]").press("Tab");
        await page.getByRole("button", { name: "Send reset link" }).click();
        await expect(page.locator("[data-reset-sent]")).toContainText(`If an account uses ${ada.email}`);

        // Administrators can read the emails when no mail server is set up.
        const adminPage = await browser.newPage();
        await signIn(adminPage);
        await adminPage.getByRole("link", { name: "Emails" }).click();
        await expect(adminPage.locator("[data-outbox-email]", { hasText: ada.email }).first()).toContainText("Reset your FormFlow password");
        await adminPage.close();

        await openBlazor(page, await emailedLink(request, ada.email, "reset-password"));
        await page.locator("input[type=password] >> nth=0").fill("difference engine");
        await page.locator("input[type=password] >> nth=0").press("Tab");
        await page.locator("input[type=password] >> nth=1").fill("difference engine");
        await page.locator("input[type=password] >> nth=1").press("Tab");
        await page.getByRole("button", { name: "Save new password" }).click();
        await expect(page.locator("[data-reset-done]")).toContainText("Your password was changed.");

        await openBlazor(page, "/login");
        await submitSignIn(page, ada.email, "difference engine");
        await expect(page).toHaveURL(/\/admin\/surveys$/);

        await page.getByRole("link", { name: "Change password" }).click();
        await expect(page).toHaveURL(/\/account$/);
        const passwords = page.locator("input[type=password]");
        await expect(passwords).toHaveCount(3);
        for (const [i, value] of ["difference engine", "analytical engine", "analytical engine"].entries()) {
            await passwords.nth(i).fill(value);
            await passwords.nth(i).press("Tab");
        }
        await page.getByRole("button", { name: "Change password" }).click();
        await expect(page.locator("[data-password-changed]")).toContainText("Your password was changed.");
        await expect(page.getByText(`Signed in as ${ada.email}`).first()).toBeVisible();
        await page.close();
    });

    test("professors see and manage only their own surveys", async ({ page, request }) => {
        const headers = await headersFor(request, professor);
        const questions: { id: string; key: string }[] = await (await request.get(`${urls.api}/api/questions`)).json();
        const title = `Lab sign-up ${runId}`;
        const created = await request.post(`${urls.api}/api/surveys`, {
            headers, data: { title, description: "Professor's survey", questionIds: [questions.find(q => q.key === "first_name")!.id] },
        });
        expect(created.status()).toBe(201);

        await signIn(page, professor);
        await expect(page.getByText(`Signed in as ${professor.username} (Professor/Scientist)`)).toBeVisible();
        await expect(questionRow(page, title)).toHaveCount(1);
        await expect(questionRow(page, demoSurveyTitle)).toHaveCount(0);

        const demo = await surveyByTitle(request, demoSurveyTitle);
        await openBlazor(page, `/admin/surveys/${demo.id}/edit`);
        await expect(page.getByText("You can only edit surveys you created.")).toBeVisible();
        await expect(page.getByRole("button", { name: "Save Survey" })).toHaveCount(0);

        // Everyone's questions can go in a survey, but only their creators can change them.
        await openQuestionBank(page);
        const firstName = page.locator("tr", { has: page.locator("td", { hasText: /^first_name$/ }) });
        await expect(firstName.getByText("Administrators")).toBeVisible();
        await expect(firstName.getByRole("link", { name: "Edit" })).toHaveCount(0);

        await openBlazor(page, "/admin/surveys");
        await confirmDelete(page, title);
        await expect(questionRow(page, title)).toHaveCount(0);
    });

    test("administrators can view the site as a professor or a student", async ({ page }) => {
        await signIn(page);
        await expect(questionRow(page, demoSurveyTitle)).toHaveCount(1);
        const viewAs = page.getByLabel("View the site as");

        await viewAs.selectOption("professor");
        await expect(page.locator("[data-view-as-banner]")).toContainText("You're viewing the site as a Professor/Scientist.");
        await expect(page).toHaveURL(/\/admin\/surveys$/);
        await expect(questionRow(page, demoSurveyTitle)).toHaveCount(0);

        await page.getByLabel("View the site as").selectOption("student");
        await expect(page).toHaveURL(/\/surveys$/);
        await expect(page.locator("[data-view-as-banner]")).toContainText("You're viewing the site as a Student.");
        await expect(page.getByRole("link", { name: "Survey Builder" })).toHaveCount(0);

        // The preview lasts across page loads until the administrator turns it off.
        await openBlazor(page, "/admin/surveys");
        await expect(page.getByText("Students can take surveys but can't open the survey builder.")).toBeVisible();
        await page.getByRole("button", { name: "Back to Administrator view" }).click();
        await expect(page).toHaveURL(/\/admin\/surveys$/);
        await expect(page.locator("[data-view-as-banner]")).toHaveCount(0);
        await expect(questionRow(page, demoSurveyTitle)).toHaveCount(1);
    });
});

// Each step builds on the previous one, so they run in order and stop at the first failure.
// They share one signed-in tab, like an admin working through the pages.
test.describe.serial("Blazor: admin", () => {
    let page: Page;

    test.beforeAll(async ({ browser }) => {
        // Clipboard access, so "Copy link" works as it does for a person clicking it.
        const context = await browser.newContext({ permissions: ["clipboard-read", "clipboard-write"] });
        page = await context.newPage();
        await signIn(page);
    });

    test.afterAll(async () => {
        await page.context().close();
    });

    test("creates a conditional question with an answer rule", async () => {
        await openBlazor(page, "/admin/questions");
        await page.getByRole("link", { name: "Create Question" }).click();
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

        await openQuestionBank(page);
        await expect(questionRow(page, key)).toHaveCount(1);
        await expect(questionRow(page, key).getByText("is_student is yes")).toBeVisible();
    });

    test("edits the question", async () => {
        await openQuestionBank(page);
        await questionRow(page, key).getByRole("link", { name: "Edit" }).click();
        await expect(page).toHaveURL(/\/edit$/);
        await expect(page.getByLabel("Minimum length")).toHaveValue("3");

        const labelField = page.getByLabel("Label").first();
        await labelField.fill(editedLabel);
        await labelField.press("Tab");
        await page.getByRole("button", { name: "Save Changes" }).click();
        await expect(page).toHaveURL(/\/admin\/questions$/);
        await showAllQuestions(page);
        await expect(page.locator("td", { hasText: editedLabel })).toHaveCount(1);
    });

    test("builds a survey and warns about question order", async () => {
        await openBlazor(page, "/admin/surveys");
        await page.getByRole("link", { name: "Create Survey" }).click();
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
        await questionRow(page, surveyTitle).getByRole("link", { name: "Preview" }).click();
        await expect(page.locator(".page[data-interactive=true]")).toBeVisible();
        await expect(question(page, "is_student")).toBeVisible();
        await expect(question(page, key)).toHaveCount(0);
        await question(page, "is_student").locator("label.mud-radio").first().click();
        await expect(question(page, key)).toHaveCount(1);
    });

    test("publishes the survey and copies its share link", async ({ browser }) => {
        await openBlazor(page, "/admin/surveys");
        const row = questionRow(page, surveyTitle);
        await expect(row.locator("[data-survey-status]")).toHaveText("Draft");
        await row.getByRole("link", { name: "Share" }).click();
        await expect(page).toHaveURL(/\/share$/);
        await expect(page.getByRole("button", { name: "Save sharing settings" })).toBeVisible();

        const linkField = page.locator("input[data-share-link], [data-share-link] input").first();
        shareLink = await linkField.inputValue();
        expect(shareLink).toMatch(/\/s\/[a-z0-9]{8}$/);
        await expect(page.locator("[data-share-qr] svg")).toBeVisible();

        // Nobody else can open a draft, even with the link.
        const visitor = await browser.newPage();
        await visitor.goto(shareLink);
        await expect(visitor.getByText("This survey does not exist or is no longer available.")).toBeVisible();

        await page.getByText("Published: anyone with the link can answer").click();
        await page.getByRole("button", { name: "Save sharing settings" }).click();
        await expect(page.getByText("Saved. The survey is published.")).toBeVisible();
        await expect(page.locator("[data-survey-status]")).toHaveText("Published, link only");

        await page.getByRole("button", { name: "Copy link" }).click();
        await expect(page.getByText("Link copied.")).toBeVisible();
        expect(await page.evaluate(() => navigator.clipboard.readText())).toBe(shareLink);

        await visitor.reload();
        await expect(visitor.getByText(surveyTitle)).toBeVisible();
        await visitor.close();
    });

    test("enforces the new question's rule when a student answers by link", async ({ browser }) => {
        // Students answer without an account, in their own browser.
        const student = await browser.newPage();
        await student.goto(shareLink);
        await expect(student.locator(".page[data-interactive=true]")).toBeVisible();
        await question(student, "is_student").locator("label.mud-radio").first().click();
        await answer(student, key, "TA");
        await student.getByRole("button", { name: "Submit" }).click();
        await expect(question(student, key).getByText("Minimum length is 3.")).toBeVisible();

        await answer(student, key, "Library assistant");
        await student.getByRole("button", { name: "Submit" }).click();
        await expect(student.getByText("Thank you!")).toBeVisible();

        await student.reload();
        await expect(student.locator("[data-survey-closed]")).toContainText("You've already answered this survey.");
        await student.close();
    });

    test("closes the survey", async ({ browser }) => {
        await openBlazor(page, "/admin/surveys");
        await questionRow(page, surveyTitle).getByRole("link", { name: "Share" }).click();
        // The survey list has status chips too, so wait until the Share page has replaced it.
        await expect(page).toHaveURL(/\/share$/);
        await expect(page.getByRole("button", { name: "Save sharing settings" })).toBeVisible();
        await expect(page.locator("[data-survey-status]")).toHaveText("Published, link only");
        const closeField = page.getByLabel("Stop taking answers at");
        await closeField.fill("2020-01-01T09:00");
        await closeField.press("Tab");
        await page.getByRole("button", { name: "Save sharing settings" }).click();
        await expect(page.locator("[data-survey-status]")).toHaveText("Closed");

        const late = await browser.newPage();
        await late.goto(shareLink);
        await expect(late.locator("[data-survey-closed]")).toContainText("This survey is closed and no longer takes answers.");
        await late.close();
    });

    test("refuses to delete a question a survey uses", async () => {
        await openQuestionBank(page);
        await confirmDelete(page, editedLabel);
        await expect(page.getByText(`This question is used by: ${surveyTitle}.`, { exact: false })).toBeVisible();
        await expect(page.locator("td", { hasText: editedLabel })).toHaveCount(1);
    });

    test("deletes the survey, then the question", async () => {
        await openBlazor(page, "/admin/surveys");
        await confirmDelete(page, surveyTitle);
        await expect(questionRow(page, surveyTitle)).toHaveCount(0);

        await openQuestionBank(page);
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
