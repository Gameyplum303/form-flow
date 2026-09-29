import React from "react";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import "@testing-library/jest-dom";
import App from "../../FormFlow.React/src/App";

const surveyId = "7cc9250a-1661-455d-ab32-7e30872b844a";
const survey = {
  id: surveyId, title: "Campus survey", description: "About you", questionIds: ["1"], createdAt: "",
  status: "published", listed: true, shareCode: "k7m2p9qa", closesAt: null as string | null,
};
const questions = [{ id: "1", key: "first_name", label: "First name", type: "text", required: true }];

type Handler = (url: string, init?: RequestInit) => { status: number; body: unknown };

function mockFetch(handler: Handler) {
  const fetchMock = jest.fn(async (url: string, init?: RequestInit) => {
    const { status, body } = handler(url, init);
    return { ok: status < 300, status, json: async () => body } as Response;
  });
  global.fetch = fetchMock as unknown as typeof fetch;
  return fetchMock;
}

function api(url: string, init?: RequestInit, submitStatus = 201) {
  if (url.endsWith("/api/surveys")) return { status: 200, body: [survey] };
  if (url.endsWith(`/api/surveys/${surveyId}`)) return { status: 200, body: survey };
  if (url.endsWith(`/api/surveys/${surveyId}/questions`)) return { status: 200, body: questions };
  if (url.endsWith("/api/share/k7m2p9qa")) return { status: 200, body: survey };
  if (url.includes(`/api/surveys/${surveyId}/answered`)) return { status: 200, body: { answered: false } };
  if (url.endsWith("/responses") && init?.method === "POST") {
    return submitStatus === 201
      ? { status: 201, body: {} }
      : { status: 400, body: { errors: { first_name: ["This question is required."] } } };
  }
  return { status: 404, body: {} };
}

describe("App", () => {
  beforeEach(() => {
    window.location.hash = "";
    localStorage.clear();
  });

  test("lists surveys, opens one, and submits answers", async () => {
    const fetchMock = mockFetch((url, init) => api(url, init));
    render(<App />);

    fireEvent.click(await screen.findByRole("button", { name: "Take survey" }));
    fireEvent.change(await screen.findByLabelText(/First name/), { target: { value: "Ada" } });
    fireEvent.click(screen.getByRole("button", { name: "Submit" }));

    expect(await screen.findByText("Thank you!")).toBeInTheDocument();
    const post = fetchMock.mock.calls.find(([, init]) => init?.method === "POST");
    const body = JSON.parse(post![1]!.body as string);
    expect(body.answers).toEqual({ first_name: ["Ada"] });
    expect(body.respondentId).toBe(localStorage.getItem("formflow.respondent"));
    expect(body.respondentId).toBeTruthy();
  });

  test("opens a survey from its share link", async () => {
    mockFetch((url, init) => api(url, init));
    window.location.hash = "#/s/k7m2p9qa";
    render(<App />);

    expect(await screen.findByRole("heading", { name: "Campus survey" })).toBeInTheDocument();
    expect(screen.getByLabelText(/First name/)).toBeInTheDocument();
  });

  test("says so when this browser already answered the survey", async () => {
    mockFetch((url, init) =>
      url.includes("/answered") ? { status: 200, body: { answered: true } } : api(url, init));
    window.location.hash = "#/s/k7m2p9qa";
    render(<App />);

    expect(await screen.findByText("You've already answered this survey. Thank you!")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Submit" })).not.toBeInTheDocument();
  });

  test("says so when the survey has closed", async () => {
    const closed = { ...survey, closesAt: "2020-01-01T00:00:00Z" };
    mockFetch((url, init) => (url.endsWith(`/api/surveys/${surveyId}`) ? { status: 200, body: closed } : api(url, init)));
    window.location.hash = `#/surveys/${surveyId}`;
    render(<App />);

    expect(await screen.findByText("This survey is closed and no longer takes answers.")).toBeInTheDocument();
  });

  test("stops taking answers when the API says the survey closed meanwhile", async () => {
    mockFetch((url, init) =>
      url.endsWith("/responses") && init?.method === "POST"
        ? { status: 409, body: { error: "This survey is closed and no longer takes answers." } }
        : api(url, init));
    window.location.hash = `#/surveys/${surveyId}`;
    render(<App />);

    fireEvent.change(await screen.findByLabelText(/First name/), { target: { value: "Ada" } });
    fireEvent.click(screen.getByRole("button", { name: "Submit" }));

    expect(await screen.findByText("This survey is closed and no longer takes answers.")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Submit" })).not.toBeInTheDocument();
  });

  test("shows that a share link doesn't lead anywhere", async () => {
    mockFetch((url, init) => api(url, init));
    window.location.hash = "#/s/nope2345";
    render(<App />);

    expect(await screen.findByText("This survey does not exist or is no longer available.")).toBeInTheDocument();
  });

  test("shows validation errors returned by the API", async () => {
    mockFetch((url, init) => api(url, init, 400));
    window.location.hash = `#/surveys/${surveyId}`;
    render(<App />);

    fireEvent.click(await screen.findByRole("button", { name: "Submit" }));

    await waitFor(() => expect(screen.getByText("This question is required.")).toBeInTheDocument());
    expect(screen.getByText("Please fix the highlighted answers.")).toBeInTheDocument();
  });

  test("explains when the API cannot be reached", async () => {
    global.fetch = jest.fn(() => Promise.reject(new Error("offline"))) as unknown as typeof fetch;
    render(<App />);

    expect(await screen.findByText(/Could not reach the API/)).toBeInTheDocument();
  });
});
