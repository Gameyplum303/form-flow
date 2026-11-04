import { APIRequestContext, expect, Page, test } from "@playwright/test";
import { headersFor, headersWhenSignInAllowed, openBlazor, professor, question, runId, signInWhenAllowed, urls } from "./helpers";

// These tests run after the others have used most of the sign-in rate limit, so they start by
// waiting for a fresh minute if they need one.
test.beforeAll(async ({ request }) => {
    test.setTimeout(90_000);
    await headersWhenSignInAllowed(request, professor);
});

/** Where screenshots go, when SCREENSHOT_DIR is set (they are for checking the look, not for the test). */
const screenshotDir = process.env.SCREENSHOT_DIR;

async function screenshot(page: Page, name: string) {
    if (screenshotDir) {
        // Lets the bars finish moving to their new lengths.
        await page.waitForTimeout(800);
        await page.screenshot({ path: `${screenshotDir}/${name}.png`, fullPage: true });
    }
}

type Survey = { id: string; title: string; shareCode: string };

/** A published lecture poll owned by the professor, with two choice questions and a text question. */
async function lecturePoll(request: APIRequestContext, title: string, showResultsToRespondents: boolean): Promise<Survey> {
    const headers = await headersFor(request, professor);
    const bank: { id: string; key: string }[] = await (await request.get(`${urls.api}/api/questions`)).json();
    const ids = ["contact_method", "skills", "first_name"].map(key => bank.find(q => q.key === key)!.id);
    const created = await request.post(`${urls.api}/api/surveys`, {
        headers, data: { title, description: "A quick poll in the lecture.", questionIds: ids },
    });
    expect(created.status(), "creating the poll").toBe(201);
    const survey: Survey = await created.json();
    const shared = await request.put(`${urls.api}/api/surveys/${survey.id}/sharing`, {
        headers, data: { status: "published", listed: false, closesAt: null, showResultsToRespondents },
    });
    expect(shared.status(), "publishing the poll").toBe(200);
    return survey;
}

async function answerByApi(request: APIRequestContext, survey: Survey, contact: string, skills: string[]) {
    const response = await request.post(`${urls.api}/api/surveys/${survey.id}/responses`, {
        data: { answers: { contact_method: contact, skills, first_name: "Student" } },
    });
    expect(response.status(), "answering through the API").toBe(201);
}

/** "k7m2p9qa" as the presenter shows it, "K7M2 P9QA". */
function displayed(code: string) {
    return `${code.slice(0, 4)} ${code.slice(4)}`.toUpperCase();
}

// The presenter tests share one signed-in tab (the sign-in lasts for the tab), as a professor would.
test.describe.serial("Live polls: the presenter view", () => {
    let page: Page;

    test.beforeAll(async ({ browser }) => {
        test.setTimeout(120_000);
        page = await browser.newPage({ viewport: { width: 1600, height: 900 } });
        await signInWhenAllowed(page, professor);
    });

    test.afterAll(async () => {
        await page.close();
    });

    test("shows the code and updates as students answer, without reloading", async ({ browser, request }) => {
        const survey = await lecturePoll(request, `Lecture poll ${runId}`, false);
        await openBlazor(page, `/admin/surveys/${survey.id}/present`);

        await expect(page.locator("[data-share-code]")).toHaveAttribute("data-share-code", survey.shareCode);
        await expect(page.locator("[data-share-code]")).toHaveText(displayed(survey.shareCode));
        await expect(page.locator("[data-join-address]")).toHaveText(new URL(urls.blazor).host + "/join");
        await expect(page.locator(".presenter-qr svg")).toBeVisible();
        await expect(page.locator("[data-connection]")).toHaveAttribute("data-connection", "live");
        await expect(page.locator("[data-response-count]")).toHaveAttribute("data-response-count", "0");

        // A reload would lose this marker.
        await page.evaluate(() => { (window as unknown as { stillHere: boolean }).stillHere = true; });

        // A student answers on their phone, in another browser, through the React app.
        const phone = await browser.newContext({ viewport: { width: 390, height: 844 } });
        const student = await phone.newPage();
        await student.goto(`${urls.react}/#/s/${survey.shareCode}`);
        await student.getByRole("group", { name: /contact method/ }).getByLabel("Phone").check();
        await student.getByRole("group", { name: /skills/ }).getByLabel("SQL").check();
        await student.getByLabel(/First Name/).fill("Grace");
        await student.getByRole("button", { name: "Submit" }).click();
        await expect(student.getByText("Thank you!")).toBeVisible();
        await phone.close();

        await expect(page.locator("[data-response-count]")).toHaveAttribute("data-response-count", "1");
        await expect(page.locator("[data-live-result=contact_method] [data-option-bar=Phone]")).toHaveAttribute("data-count", "1");

        // More answers arrive through the API, and the charts follow.
        await answerByApi(request, survey, "email", ["sql", "csharp"]);
        await answerByApi(request, survey, "email", ["design"]);
        await answerByApi(request, survey, "text", ["sql"]);
        await expect(page.locator("[data-response-count]")).toHaveAttribute("data-response-count", "4");
        await expect(page.locator("[data-live-result=contact_method] [data-option-bar=Email]")).toContainText("2 (50%)");
        await expect(page.locator("[data-live-result=skills] [data-option-bar=SQL]")).toContainText("3 (75%)");
        // Written answers stay off the projector.
        await expect(page.locator("[data-live-result=first_name]")).toContainText("Written answers are on the results page.");
        await expect(page.getByText("Grace")).toHaveCount(0);

        expect(await page.evaluate(() => (window as unknown as { stillHere?: boolean }).stillHere)).toBe(true);
        await screenshot(page, "presenter");
    });

    test("is reached from the surveys list and the Share page", async ({ request }) => {
        const survey = await lecturePoll(request, `Linked poll ${runId}`, false);
        await openBlazor(page, "/admin/surveys");

        await page.getByRole("link", { name: `Present ${survey.title} live` }).click();
        await expect(page).toHaveURL(new RegExp(`/admin/surveys/${survey.id}/present$`));
        await expect(page.locator("[data-share-code]")).toHaveText(displayed(survey.shareCode));

        await openBlazor(page, `/admin/surveys/${survey.id}/share`);
        await page.getByRole("link", { name: "Present live" }).click();
        await expect(page).toHaveURL(new RegExp(`/admin/surveys/${survey.id}/present$`));
    });

    test("offers to publish a draft", async ({ request }) => {
        const headers = await headersFor(request, professor);
        const survey = await lecturePoll(request, `Draft poll ${runId}`, false);
        await request.put(`${urls.api}/api/surveys/${survey.id}/sharing`, {
            headers, data: { status: "draft", listed: false, closesAt: null, showResultsToRespondents: false },
        });
        await openBlazor(page, `/admin/surveys/${survey.id}/present`);

        await expect(page.locator("[data-presenter-notice]")).toContainText("This survey is a draft");
        await page.getByRole("button", { name: "Publish now" }).click();

        await expect(page.locator("[data-presenter-notice]")).toHaveCount(0);
        const opened = await request.get(`${urls.api}/api/share/${survey.shareCode}`);
        expect(opened.status(), "anyone can open it once published").toBe(200);
    });
});

test.describe("Live polls: joining by code", () => {
    test("works in the Blazor app, from the home page", async ({ page, request }) => {
        const survey = await lecturePoll(request, `Blazor join ${runId}`, false);
        await page.setViewportSize({ width: 390, height: 844 });
        await openBlazor(page, "/");
        await page.getByRole("link", { name: "Join with a code" }).first().click();
        await expect(page).toHaveURL(/\/join$/);
        await expect(page.locator(".page[data-interactive=true]")).toBeVisible();

        const code = page.locator("input[data-join-code]");
        await code.fill("nope");
        await page.getByRole("button", { name: "Join" }).click();
        await expect(page.getByText("That doesn't look like a survey code.")).toBeVisible();

        // Typed as it reads off the projector: capitals and a space.
        await code.fill(displayed(survey.shareCode));
        await screenshot(page, "join-blazor");
        await page.getByRole("button", { name: "Join" }).click();

        await expect(page).toHaveURL(new RegExp(`/s/${survey.shareCode}$`));
        await expect(page.getByRole("heading", { name: survey.title })).toBeVisible();
    });

    test("works in the React app, from its start screen", async ({ page, request }) => {
        const survey = await lecturePoll(request, `React join ${runId}`, false);
        await page.setViewportSize({ width: 390, height: 844 });
        await page.goto(urls.react);
        await page.getByRole("button", { name: "Join with a code" }).click();
        await expect(page).toHaveURL(/#\/join$/);

        await page.getByLabel("Survey code").fill("k7m2p9qb");
        await page.getByRole("button", { name: "Join", exact: true }).click();
        await expect(page.getByRole("alert")).toContainText("No open survey has that code.");

        await page.getByLabel("Survey code").fill(` ${displayed(survey.shareCode).replace(" ", "-")} `);
        await screenshot(page, "join-react");
        await page.getByRole("button", { name: "Join", exact: true }).click();

        await expect(page).toHaveURL(new RegExp(`#/s/${survey.shareCode}$`));
        await expect(page.getByRole("heading", { name: survey.title })).toBeVisible();
    });
});

test.describe("Live polls: results for respondents", () => {
    test("the React thank-you screen shows the results when the survey shares them", async ({ page, request }) => {
        const survey = await lecturePoll(request, `Shared results ${runId}`, true);
        await answerByApi(request, survey, "email", ["sql"]);
        await answerByApi(request, survey, "phone", ["sql", "design"]);
        await page.setViewportSize({ width: 390, height: 844 });
        await page.goto(`${urls.react}/#/s/${survey.shareCode}`);

        await page.getByRole("group", { name: /contact method/ }).getByLabel("Email").check();
        await page.getByRole("group", { name: /skills/ }).getByLabel("C#").check();
        await page.getByLabel(/First Name/).fill("Ada Secret");
        await page.getByRole("button", { name: "Submit" }).click();

        await expect(page.getByText("Thank you!")).toBeVisible();
        const summary = page.locator("[data-respondent-results]");
        await expect(summary).toContainText("Based on 3 responses");
        await expect(summary.locator("[data-respondent-result=contact_method] [data-option-bar=Email]")).toContainText("2 (67%)");
        await expect(summary.locator("[data-respondent-result=skills]")).toContainText("People could pick more than one.");
        // Written answers are never shared.
        await expect(summary.locator("[data-respondent-result=first_name]")).toHaveCount(0);
        await expect(page.getByText("Ada Secret")).toHaveCount(0);
        await screenshot(page, "respondent-results-react");
    });

    test("the Blazor thank-you screen shows them too, and only when shared", async ({ page, request }) => {
        const shared = await lecturePoll(request, `Blazor shared ${runId}`, true);
        const kept = await lecturePoll(request, `Blazor private ${runId}`, false);
        await answerByApi(request, shared, "phone", ["design"]);
        await page.setViewportSize({ width: 1280, height: 900 });

        for (const survey of [shared, kept]) {
            await openBlazor(page, `/s/${survey.shareCode}`);
            await question(page, "contact_method").locator("label.mud-radio").first().click();
            await question(page, "skills").locator("input[type=checkbox]").first().check();
            const name = question(page, "first_name").locator("input");
            await name.fill("Grace");
            await name.press("Tab");
            await page.getByRole("button", { name: "Submit" }).click();
            await expect(page.getByRole("heading", { name: "Thank you!" })).toBeVisible();
            if (survey === shared) {
                await expect(page.locator("[data-respondent-total]")).toContainText("Based on 2 responses");
                await expect(page.locator("[data-respondent-result=contact_method] [data-option-bar=Email]")).toContainText("1 (50%)");
                await screenshot(page, "respondent-results-blazor");
            } else {
                await expect(page.locator("[data-respondent-results]")).toHaveCount(0);
            }
        }

        const refused = await request.get(`${urls.api}/api/surveys/${kept.id}/results/public`);
        expect(refused.status(), "a survey that keeps its results private has no public summary").toBe(404);
    });
});
