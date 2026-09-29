import { APIRequestContext, expect, Locator, Page } from "@playwright/test";
import { urls } from "../playwright.config";

export { urls };

/** A suffix that keeps names unique when the tests run again against the same database. */
export const runId = Date.now().toString(36);

/** Opens a Blazor page and waits until its circuit is connected, so clicks are handled. */
export async function openBlazor(page: Page, path: string) {
    // Browser-side problems (a script that fails to load, a refused WebSocket) only show up
    // in the console, so collect them for the failure message.
    const problems: string[] = [];
    page.on("console", m => { if (m.type() === "error" || m.type() === "warning") problems.push(`${m.type()}: ${m.text()}`); });
    page.on("requestfailed", r => problems.push(`request failed: ${r.url()} ${r.failure()?.errorText}`));
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
