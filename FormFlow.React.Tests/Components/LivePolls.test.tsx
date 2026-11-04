import React from "react";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import "@testing-library/jest-dom";
import App from "../../FormFlow.React/src/App";

const surveyId = "7cc9250a-1661-455d-ab32-7e30872b844a";
const survey = {
    id: surveyId, title: "Week 3 check-in", description: "How is it going?", questionIds: ["1"], createdAt: "",
    status: "published", listed: false, shareCode: "k7m2p9qa", closesAt: null as string | null,
    showResultsToRespondents: true,
};
const questions = [{
    id: "1", key: "pace", label: "How is the pace?", type: "radio", required: true,
    options: [{ value: "slow", label: "Too slow" }, { value: "right", label: "About right" }],
}];
const summary = (total: number) => ({
    surveyId, title: survey.title, totalResponses: total,
    questions: [{
        key: "pace", label: "How is the pace?", type: "radio", answeredCount: total, multipleAnswers: false,
        options: [
            { value: "slow", label: "Too slow", count: 1, percent: Math.round(100 / total) },
            { value: "right", label: "About right", count: total - 1, percent: 100 - Math.round(100 / total) },
        ],
    }],
});

type Reply = { status: number; body: unknown };

function mockFetch(overrides: (url: string, init?: RequestInit) => Reply | undefined = () => undefined, current = survey) {
    const fetchMock = jest.fn(async (url: string, init?: RequestInit) => {
        const { status, body } = overrides(url, init) ?? api(url, init, current);
        return { ok: status < 300, status, json: async () => body } as Response;
    });
    global.fetch = fetchMock as unknown as typeof fetch;
    return fetchMock;
}

function api(url: string, init: RequestInit | undefined, current: typeof survey): Reply {
    if (url.endsWith("/api/surveys")) return { status: 200, body: [] };
    if (url.endsWith("/api/share/k7m2p9qa")) return { status: 200, body: current };
    if (url.endsWith(`/api/surveys/${surveyId}/questions`)) return { status: 200, body: questions };
    if (url.includes("/answered")) return { status: 200, body: { answered: false } };
    if (url.endsWith("/responses") && init?.method === "POST") return { status: 201, body: {} };
    if (url.endsWith("/results/public")) {
        return current.showResultsToRespondents ? { status: 200, body: summary(4) } : { status: 404, body: {} };
    }
    return { status: 404, body: {} };
}

async function answerAndSubmit() {
    fireEvent.click(await screen.findByLabelText("About right"));
    fireEvent.click(screen.getByRole("button", { name: "Submit" }));
    expect(await screen.findByText("Thank you!")).toBeInTheDocument();
}

describe("Joining by code", () => {
    beforeEach(() => {
        window.location.hash = "";
        localStorage.clear();
    });

    test("the start screen links to the Join screen", async () => {
        mockFetch();
        render(<App />);

        fireEvent.click(screen.getByRole("button", { name: "Join with a code" }));

        expect(window.location.hash).toBe("#/join");
        expect(await screen.findByRole("heading", { name: "Join a survey" })).toBeInTheDocument();
    });

    test("a typed code opens its survey, whatever its case and spacing", async () => {
        const fetchMock = mockFetch();
        window.location.hash = "#/join";
        render(<App />);

        fireEvent.change(screen.getByLabelText("Survey code"), { target: { value: " K7M2 p9qa " } });
        fireEvent.click(screen.getByRole("button", { name: "Join" }));

        expect(await screen.findByRole("heading", { name: "Week 3 check-in" })).toBeInTheDocument();
        expect(window.location.hash).toBe("#/s/k7m2p9qa");
        expect(fetchMock.mock.calls.some(([url]) => (url as string).endsWith("/api/share/k7m2p9qa"))).toBe(true);
    });

    test("a code that isn't one says so without asking the API", async () => {
        const fetchMock = mockFetch();
        window.location.hash = "#/join";
        render(<App />);

        fireEvent.change(screen.getByLabelText("Survey code"), { target: { value: "k7m2" } });
        fireEvent.click(screen.getByRole("button", { name: "Join" }));

        expect(await screen.findByRole("alert")).toHaveTextContent("That doesn't look like a survey code.");
        expect(fetchMock.mock.calls.some(([url]) => (url as string).includes("/api/share/"))).toBe(false);
    });

    test("an unknown code says so and stays on the Join screen", async () => {
        mockFetch();
        window.location.hash = "#/join";
        render(<App />);

        fireEvent.change(screen.getByLabelText("Survey code"), { target: { value: "k7m2p9qb" } });
        fireEvent.click(screen.getByRole("button", { name: "Join" }));

        expect(await screen.findByRole("alert")).toHaveTextContent("No open survey has that code.");
        expect(window.location.hash).toBe("#/join");
    });

    test("the code box suits a phone keyboard", () => {
        mockFetch();
        window.location.hash = "#/join";
        render(<App />);

        const input = screen.getByLabelText("Survey code");
        expect(input).toHaveAttribute("autocomplete", "off");
        expect(input).toHaveAttribute("autocapitalize", "characters");
        expect(input).toHaveAttribute("spellcheck", "false");
    });
});

describe("Results after answering", () => {
    beforeEach(() => {
        window.location.hash = "#/s/k7m2p9qa";
        localStorage.clear();
    });

    test("shows the results so far when the survey shares them", async () => {
        mockFetch();
        render(<App />);

        await answerAndSubmit();

        expect(await screen.findByText("Results so far")).toBeInTheDocument();
        expect(screen.getByText("Based on 4 responses")).toBeInTheDocument();
        expect(screen.getByText("3 (75%)")).toBeInTheDocument();
    });

    test("shows nothing more when the survey keeps its results private", async () => {
        const fetchMock = mockFetch(undefined, { ...survey, showResultsToRespondents: false });
        render(<App />);

        await answerAndSubmit();

        expect(screen.queryByText("Results so far")).not.toBeInTheDocument();
        expect(fetchMock.mock.calls.some(([url]) => (url as string).endsWith("/results/public"))).toBe(false);
    });

    test("Show the latest reloads the summary", async () => {
        let total = 4;
        mockFetch((url) => (url.endsWith("/results/public") ? { status: 200, body: summary(total) } : undefined));
        render(<App />);
        await answerAndSubmit();
        expect(await screen.findByText("Based on 4 responses")).toBeInTheDocument();

        total = 10;
        fireEvent.click(screen.getByRole("button", { name: "Show the latest" }));

        await waitFor(() => expect(screen.getByText("Based on 10 responses")).toBeInTheDocument());
    });

    test("a browser that already answered still sees the shared results", async () => {
        mockFetch((url) => (url.includes("/answered") ? { status: 200, body: { answered: true } } : undefined));
        render(<App />);

        expect(await screen.findByText("You've already answered this survey. Thank you!")).toBeInTheDocument();
        expect(await screen.findByText("Results so far")).toBeInTheDocument();
    });
});
