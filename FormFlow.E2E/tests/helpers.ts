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

/** Publishes a survey through the API, since new surveys start as private drafts. */
export async function publish(
    request: APIRequestContext, surveyId: string, headers: Record<string, string>,
    sharing: { listed?: boolean; closesAt?: string | null } = {},
) {
    const response = await request.put(`${urls.api}/api/surveys/${surveyId}/sharing`, {
        headers, data: { status: "published", listed: sharing.listed ?? false, closesAt: sharing.closesAt ?? null },
    });
    expect(response.status(), "publishing the survey").toBe(200);
    return response.json();
}

export const demoSurveyTitle = "Student Experience Survey";

/** The admin account docker compose creates. Override with ADMIN_USERNAME and ADMIN_PASSWORD. */
export const admin = {
    username: process.env.ADMIN_USERNAME ?? "Rogers",
    password: process.env.ADMIN_PASSWORD ?? "password",
};

/** The professor/scientist test account. Override with PROFESSOR_USERNAME and PROFESSOR_PASSWORD. */
export const professor = {
    username: process.env.PROFESSOR_USERNAME ?? "professor",
    password: process.env.PROFESSOR_PASSWORD ?? "password",
};

/** A new professor/scientist sign-up, with an email unique to this run. */
export function newSignUp(name: string) {
    return {
        name,
        email: `${name.toLowerCase().replace(/[^a-z]/g, "")}.${runId}@lab.example`,
        password: "analytical",
        dateOfBirth: "1990-12-10",
        intendedUse: `Surveys for ${name}'s studies.`,
        organization: "Analytical Engines Lab",
    };
}

const tokens = new Map<string, string>();

/** Headers for API calls made as an account, signing in once per test run. */
export async function headersFor(request: APIRequestContext, account: { username: string; password: string }) {
    if (!tokens.has(account.username)) {
        const response = await request.post(`${urls.api}/api/auth/login`, { data: account });
        expect(response.status(), `${account.username} sign-in through the API`).toBe(200);
        tokens.set(account.username, (await response.json()).token);
    }
    return { Authorization: `Bearer ${tokens.get(account.username)}` };
}

/** Headers for API calls that need an admin. */
export function adminHeaders(request: APIRequestContext) {
    return headersFor(request, admin);
}

/**
 * The path (like "/verify-email?token=...") of the newest link to a Blazor page emailed to an address.
 * Without a mail server the API keeps emails in an outbox that administrators can read. The path is
 * opened against BLAZOR_URL, whatever address the API puts in its links.
 */
export async function emailedLink(request: APIRequestContext, to: string, page: "verify-email" | "reset-password") {
    const headers = await adminHeaders(request);
    let path: string | undefined;
    await expect.poll(async () => {
        const outbox: { to: string; body: string }[] = await (await request.get(`${urls.api}/api/accounts/outbox`, { headers })).json();
        const email = outbox.find(e => e.to.toLowerCase() === to.toLowerCase() && e.body.includes(`/${page}?token=`));
        path = email?.body.match(new RegExp(`/${page}\\?token=\\S+`))?.[0];
        return path;
    }, { message: `a ${page} email to ${to}` }).toBeDefined();
    return path!;
}

/** The token in an emailed link's path. */
export function tokenOf(path: string) {
    return decodeURIComponent(new URL(path, "http://x").searchParams.get("token")!);
}

/** A professor who signed up, verified their email and was approved, all through the API. */
export async function approvedProfessor(request: APIRequestContext, name: string) {
    const signUp = newSignUp(name);
    expect((await request.post(`${urls.api}/api/auth/signup`, { data: signUp })).status()).toBe(201);
    const verify = await request.post(`${urls.api}/api/auth/verify-email`, {
        data: { token: tokenOf(await emailedLink(request, signUp.email, "verify-email")) },
    });
    expect(verify.status()).toBe(200);
    const headers = await adminHeaders(request);
    const pending: { id: string; email: string }[] = await (await request.get(`${urls.api}/api/accounts/pending`, { headers })).json();
    const id = pending.find(p => p.email === signUp.email)!.id;
    expect((await request.post(`${urls.api}/api/accounts/${id}/approve`, { headers })).status()).toBe(204);
    return signUp;
}

/** Fills in the Blazor sign-in form on the current page. */
export async function submitSignIn(page: Page, username: string, password: string) {
    await page.locator("input[autocomplete=username]").fill(username);
    await page.locator("input[autocomplete=username]").press("Tab");
    await page.locator("input[type=password]").fill(password);
    await page.locator("input[type=password]").press("Tab");
    await page.getByRole("button", { name: "Sign in" }).click();
}

/** Signs in through the Blazor sign-in page, landing on the survey builder. The sign-in lasts for this tab, across page loads. */
export async function signIn(page: Page, account = admin, landsOn = /\/admin\/surveys$/) {
    await openBlazor(page, "/login");
    await submitSignIn(page, account.username, account.password);
    await expect(page).toHaveURL(landsOn);
}
