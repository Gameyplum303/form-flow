import React from "react";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import "@testing-library/jest-dom";
import App from "../../FormFlow.React/src/App";

const surveyId = "7cc9250a-1661-455d-ab32-7e30872b844a";
const survey = { id: surveyId, title: "Campus survey", description: "About you", questionIds: ["1"], createdAt: "" };
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
  });

  test("lists surveys, opens one, and submits answers", async () => {
    const fetchMock = mockFetch((url, init) => api(url, init));
    render(<App />);

    fireEvent.click(await screen.findByRole("button", { name: "Take survey" }));
    fireEvent.change(await screen.findByLabelText(/First name/), { target: { value: "Ada" } });
    fireEvent.click(screen.getByRole("button", { name: "Submit" }));

    expect(await screen.findByText("Thank you!")).toBeInTheDocument();
    const post = fetchMock.mock.calls.find(([, init]) => init?.method === "POST");
    expect(JSON.parse(post![1]!.body as string)).toEqual({ answers: { first_name: ["Ada"] } });
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
