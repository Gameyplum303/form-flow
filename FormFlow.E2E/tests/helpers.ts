import { APIRequestContext, expect, Locator, Page } from "@playwright/test";
import { urls } from "../playwright.config";

export { urls };

/** A suffix that keeps names unique when the tests run again against the same database. */
export const runId = Date.now().toString(36);

const problemsByPage = new WeakMap<Page, string[]>();

/**
 * Browser-side problems (a script that fails to load, a refused WebSocket) only show up
 * in the console, so they are collected for failure messages.
 */
function browserProblems(page: Page): string[] {
    let problems = problemsByPage.get(page);
    if (!problems) {
        const list: string[] = [];
        page.on("console", m => { if (m.type() === "error" || m.type() === "warning") list.push(`${m.type()}: ${m.text()}`); });
        page.on("requestfailed", r => list.push(`request failed: ${r.url()} ${r.failure()?.errorText}`));
        problemsByPage.set(page, list);
        problems = list;
    }
    return problems;
}

/** Opens a Blazor page and waits until its circuit is connected, so clicks are handled. */
export async function openBlazor(page: Page, path: string) {
    const problems = browserProblems(page);
    problems.length = 0;
    await page.goto(urls.blazor + path);
    await expect(page.locator(".page[data-interactive=true]"),
        `Blazor did not become interactive. Browser messages:\n${problems.join("\n") || "(none)"}`).toBeVisible();
}

export function question(page: Page, key: string): Locator {
    return page.locator(`[data-question-key=${key}]`);
}

/** Types into a Blazor question's input and leaves the field, which is when MudBlazor reports the value. */
export async function answer(page: Page, key: string, value: string) {
    const input = question(page, key).locator("input").first();
    await input.fill(value);
    await input.press("Tab");
}

/** Picks an item from a MudSelect. */
export async function choose(page: Page, select: Locator, item: string) {
    await select.first().click();
    await page.locator(".mud-popover-open .mud-list-item", { hasText: item }).first().click();
    await expect(page.locator(".mud-popover-open")).toHaveCount(0);
}

export async function surveyByTitle(request: APIRequestContext, title: string) {
    const surveys: { id: string; title: string }[] = await (await request.get(`${urls.api}/api/surveys`)).json();
    const survey = surveys.find(s => s.title === title);
    expect(survey, `survey "${title}"`).toBeDefined();
    return survey!;
}

export const demoSurveyTitle = "Student Experience Survey";

/** The test account docker compose creates. Override with ADMIN_USERNAME and ADMIN_PASSWORD. */
export const admin = {
    username: process.env.ADMIN_USERNAME ?? "student",
    password: process.env.ADMIN_PASSWORD ?? "password",
};

let adminToken: string | undefined;

/** Headers for API calls that need an admin, signing in once per test run. */
export async function adminHeaders(request: APIRequestContext) {
    if (!adminToken) {
        const response = await request.post(`${urls.api}/api/auth/login`, { data: admin });
        expect(response.status(), "admin sign-in through the API").toBe(200);
        adminToken = (await response.json()).token;
    }
    return { Authorization: `Bearer ${adminToken}` };
}

/** Fills in the Blazor sign-in form on the current page. */
export async function submitSignIn(page: Page, username: string, password: string) {
    await page.locator("input[autocomplete=username]").fill(username);
    await page.locator("input[autocomplete=username]").press("Tab");
    await page.locator("input[type=password]").fill(password);
    await page.locator("input[type=password]").press("Tab");
    await page.getByRole("button", { name: "Sign in" }).click();
}

/** Signs in to the Blazor admin pages. The sign-in lasts for this tab, across page loads. */
export async function signIn(page: Page) {
    await openBlazor(page, "/login");
    await submitSignIn(page, admin.username, admin.password);
    await expect(page).toHaveURL(/\/admin\/surveys$/);
}
