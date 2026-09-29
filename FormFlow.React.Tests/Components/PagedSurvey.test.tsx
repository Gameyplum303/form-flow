import React from "react";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import "@testing-library/jest-dom";
import App from "../../FormFlow.React/src/App";

// Page 1: name and is_student. Page 2: campus, for students only. Page 3: comments.
const surveyId = "3b0f6f7e-8c1d-4a55-9d6e-2f1c0b7a9e11";
const questions = [
  { id: "q1", key: "name", label: "Your name", type: "text", required: true },
  { id: "q2", key: "is_student", label: "Are you a student?", type: "yes_no", required: true },
  { id: "q3", key: "campus", label: "Which campus?", type: "text", required: true, visibleIf: { key: "is_student", shouldEqual: true } },
  { id: "q4", key: "comments", label: "Any comments?", type: "text", required: false },
];
const survey = {
  id: surveyId, title: "Campus survey", description: "Three pages", questionIds: ["q1", "q2", "q3", "q4"], createdAt: "",
  status: "published", listed: true, shareCode: "p4g3d2ab", closesAt: null, pageBreaks: ["q3", "q4"],
};
const draftKey = `formflow.answers.${surveyId}`;

type Reply = { status: number; body: unknown };

function mockFetch(submit: () => Reply = () => ({ status: 201, body: {} }), current = survey) {
  const fetchMock = jest.fn(async (url: string, init?: RequestInit) => {
    let reply: Reply = { status: 404, body: {} };
    if (url.endsWith(`/api/surveys/${surveyId}`)) reply = { status: 200, body: current };
    else if (url.endsWith(`/api/surveys/${surveyId}/questions`)) reply = { status: 200, body: questions };
    else if (url.includes("/answered")) reply = { status: 200, body: { answered: false } };
    else if (url.endsWith("/responses") && init?.method === "POST") reply = submit();
    return { ok: reply.status < 300, status: reply.status, json: async () => reply.body } as Response;
  });
  global.fetch = fetchMock as unknown as typeof fetch;
  return fetchMock;
}

async function openSurvey() {
  window.location.hash = `#/surveys/${surveyId}`;
  render(<App />);
  return screen.findByRole("heading", { name: "Campus survey" });
}

const pageLabel = () => screen.getByText(/^Page \d of \d$/).textContent;
const button = (name: string) => screen.queryByRole("button", { name });
const answerStudent = (answer: "Yes" | "No") => fireEvent.click(screen.getByLabelText(answer));

describe("Paged surveys", () => {
  beforeEach(() => {
    window.location.hash = "";
    localStorage.clear();
    window.scrollTo = jest.fn();
  });

  test("shows one page at a time with its progress", async () => {
    mockFetch();
    await openSurvey();

    // The campus page only counts once someone says they're a student.
    expect(pageLabel()).toBe("Page 1 of 2");
    expect(screen.getByRole("progressbar")).toHaveAttribute("value", "1");
    expect(screen.getByRole("progressbar")).toHaveAttribute("max", "2");
    expect(screen.getByLabelText(/Your name/)).toBeInTheDocument();
    expect(screen.queryByLabelText(/Any comments/)).not.toBeInTheDocument();
    expect(button("Next")).toBeInTheDocument();
    expect(button("Submit")).not.toBeInTheDocument();
    expect(button("Back")).not.toBeInTheDocument();
  });

  test("Next is blocked until the page's required questions are answered", async () => {
    const fetchMock = mockFetch();
    await openSurvey();
    fireEvent.change(screen.getByLabelText(/Your name/), { target: { value: "Ada" } });

    fireEvent.click(button("Next")!);

    expect(await screen.findByText("This question is required.")).toBeInTheDocument();
    expect(pageLabel()).toBe("Page 1 of 2");
    expect(fetchMock.mock.calls.some(([, init]) => init?.method === "POST")).toBe(false);

    answerStudent("No");
    fireEvent.click(button("Next")!);

    await waitFor(() => expect(pageLabel()).toBe("Page 2 of 2"));
    expect(screen.queryByText("This question is required.")).not.toBeInTheDocument();
  });

  test("Back keeps the answers, and a conditional page shows for students", async () => {
    mockFetch();
    await openSurvey();
    fireEvent.change(screen.getByLabelText(/Your name/), { target: { value: "Ada" } });
    answerStudent("Yes");
    expect(pageLabel()).toBe("Page 1 of 3");

    fireEvent.click(button("Next")!);
    await waitFor(() => expect(pageLabel()).toBe("Page 2 of 3"));
    expect(screen.getByLabelText(/Which campus/)).toBeInTheDocument();

    fireEvent.click(button("Back")!);

    await waitFor(() => expect(pageLabel()).toBe("Page 1 of 3"));
    expect(screen.getByLabelText(/Your name/)).toHaveValue("Ada");
    expect(screen.getByLabelText("Yes")).toBeChecked();
  });

  test("skips a hidden page and submits every answer from the last page", async () => {
    const fetchMock = mockFetch();
    await openSurvey();
    fireEvent.change(screen.getByLabelText(/Your name/), { target: { value: "Ada" } });
    answerStudent("No");
    fireEvent.click(button("Next")!);
    await waitFor(() => expect(pageLabel()).toBe("Page 2 of 2"));
    expect(screen.getByLabelText(/Any comments/)).toBeInTheDocument();
    expect(button("Next")).not.toBeInTheDocument();

    fireEvent.click(button("Submit")!);

    expect(await screen.findByText("Thank you!")).toBeInTheDocument();
    const post = fetchMock.mock.calls.find(([, init]) => init?.method === "POST");
    expect(JSON.parse(post![1]!.body as string).answers).toEqual({ name: ["Ada"], is_student: ["false"] });
  });

  test("goes to the first page with an error when the server rejects the answers", async () => {
    mockFetch(() => ({ status: 400, body: { errors: { name: ["Minimum length is 3."] } } }));
    await openSurvey();
    fireEvent.change(screen.getByLabelText(/Your name/), { target: { value: "Al" } });
    answerStudent("No");
    fireEvent.click(button("Next")!);
    await waitFor(() => expect(pageLabel()).toBe("Page 2 of 2"));

    fireEvent.click(button("Submit")!);

    expect(await screen.findByText("Minimum length is 3.")).toBeInTheDocument();
    expect(pageLabel()).toBe("Page 1 of 2");
    expect(screen.getByLabelText(/Your name/)).toHaveValue("Al");
  });

  test("shows no progress while answers leave only one page", async () => {
    // Everything after the first page is for students only.
    const studentsOnly = questions.map((q) => (q.key === "comments" ? { ...q, visibleIf: { key: "is_student", shouldEqual: true } } : q));
    const fetchMock = jest.fn(async (url: string) => {
      const body = url.endsWith("/questions") ? studentsOnly : url.includes("/answered") ? { answered: false } : survey;
      return { ok: true, status: 200, json: async () => body } as Response;
    });
    global.fetch = fetchMock as unknown as typeof fetch;
    await openSurvey();

    expect(screen.queryByText(/^Page \d of \d$/)).not.toBeInTheDocument();
    expect(button("Submit")).toBeInTheDocument();

    answerStudent("Yes");

    expect(pageLabel()).toBe("Page 1 of 3");
    expect(button("Next")).toBeInTheDocument();
  });

  test("a survey without page breaks is one page with Submit", async () => {
    mockFetch(undefined, { ...survey, pageBreaks: [] });
    await openSurvey();

    expect(screen.queryByText(/^Page \d of \d$/)).not.toBeInTheDocument();
    expect(screen.getByLabelText(/Any comments/)).toBeInTheDocument();
    expect(button("Submit")).toBeInTheDocument();
    expect(button("Next")).not.toBeInTheDocument();
  });
});

describe("Saved answers", () => {
  beforeEach(() => {
    window.location.hash = "";
    localStorage.clear();
    window.scrollTo = jest.fn();
  });

  test("are kept while answering and cleared after submitting", async () => {
    mockFetch(undefined, { ...survey, pageBreaks: [] });
    await openSurvey();

    fireEvent.change(screen.getByLabelText(/Your name/), { target: { value: "Ada" } });
    answerStudent("No");
    expect(JSON.parse(localStorage.getItem(draftKey)!)).toEqual({ name: ["Ada"], is_student: ["false"] });

    fireEvent.click(button("Submit")!);

    expect(await screen.findByText("Thank you!")).toBeInTheDocument();
    expect(localStorage.getItem(draftKey)).toBeNull();
  });

  test("come back with a note, and Start over clears them", async () => {
    localStorage.setItem(draftKey, JSON.stringify({ name: ["Ada"], is_student: ["true"], removed_question: ["x"] }));
    mockFetch();
    await openSurvey();

    expect(screen.getByText(/We saved your answers on this device/)).toBeInTheDocument();
    expect(screen.getByLabelText(/Your name/)).toHaveValue("Ada");
    expect(screen.getByLabelText("Yes")).toBeChecked();

    fireEvent.click(button("Start over")!);

    expect(screen.queryByText(/We saved your answers/)).not.toBeInTheDocument();
    expect(screen.getByLabelText(/Your name/)).toHaveValue("");
    expect(localStorage.getItem(draftKey)).toBeNull();
  });

  test("that can't be read are ignored", async () => {
    localStorage.setItem(draftKey, "{not json");
    mockFetch();
    await openSurvey();

    expect(screen.queryByText(/We saved your answers/)).not.toBeInTheDocument();
    expect(screen.getByLabelText(/Your name/)).toHaveValue("");
  });

  test("still let people answer when storage is blocked", async () => {
    const setItem = jest.spyOn(Storage.prototype, "setItem").mockImplementation(() => {
      throw new Error("QuotaExceededError");
    });
    try {
      mockFetch(undefined, { ...survey, pageBreaks: [] });
      await openSurvey();

      fireEvent.change(screen.getByLabelText(/Your name/), { target: { value: "Ada" } });

      expect(screen.getByLabelText(/Your name/)).toHaveValue("Ada");
    } finally {
      setItem.mockRestore();
    }
  });
});
