import { defineConfig, devices } from "@playwright/test";

// The tests run against apps that are already running, by default the docker compose
// stack (docker compose up --build). Override the addresses with these variables.
export const urls = {
    api: process.env.API_URL ?? "http://localhost:5164",
    blazor: process.env.BLAZOR_URL ?? "http://localhost:5224",
    react: process.env.REACT_URL ?? "http://localhost:3000",
};

export default defineConfig({
    testDir: "./tests",
    // The tests share one database, so they run one at a time.
    workers: 1,
    fullyParallel: false,
    forbidOnly: !!process.env.CI,
    timeout: 60_000,
    expect: { timeout: 10_000 },
    reporter: process.env.CI ? [["list"], ["html", { open: "never" }]] : "list",
    use: {
        ...devices["Desktop Chrome"],
        trace: "retain-on-failure",
        screenshot: "only-on-failure",
        launchOptions: { executablePath: process.env.PW_CHROMIUM_PATH || undefined },
    },
});
