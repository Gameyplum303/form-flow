import React from "react";
import { fireEvent, render, screen, within } from "@testing-library/react";
import "@testing-library/jest-dom";
import App from "../../FormFlow.React/src/App";
import { formatPoints, formatScore, QuizScore } from "../../FormFlow.React/src/components/QuizScore";
import { QuizResult } from "../../FormFlow.React/src/types/Quiz";

const surveyId = "3f0e3c8e-8a52-4a6f-9c39-6a3f2b1d7e40";
const quiz = {
    id: surveyId, title: "Geography quiz", description: "No notes", questionIds: ["1"], createdAt: "",
    status: "published", listed: true, isQuiz: true, quizFeedback: "answers",
};
const questions = [{ id: "1", key: "capital", label: "Capital of France?", type: "text", required: true }];

const graded: QuizResult = {
    score: 1.5,
    maxScore: 3,
    percent: 50,
    questions: [
        {
            key: "capital", label: "Capital of France?", points: 1, maxPoints: 1, correct: true,
            givenAnswers: ["Paris"], correctAnswers: ["Paris"], explanation: "Since the tenth century.",
        },
        {
            key: "primes", label: "Which are prime?", points: 0.5, maxPoints: 1, correct: false,
            givenAnswers: ["Two"], correctAnswers: ["Two", "Three"],
        },
        {
            key: "sum", label: "What is 2 + 2?", points: 0, maxPoints: 1, correct: false,
            givenAnswers: [], correctAnswers: ["4"], explanation: null,
        },
    ],
};

function mockApi(created: unknown) {
    global.fetch = jest.fn(async (url: string, init?: RequestInit) => {
        const reply = (status: number, body: unknown) => ({ ok: status < 300, status, json: async () => body }) as Response;
        if (url.endsWith(`/api/surveys/${surveyId}`)) return reply(200, quiz);
        if (url.endsWith(`/api/surveys/${surveyId}/questions`)) return reply(200, questions);
        if (url.includes("/answered")) return reply(200, { answered: false });
        if (url.endsWith("/responses") && init?.method === "POST") return reply(201, created);
        return reply(404, {});
    }) as unknown as typeof fetch;
}

async function submitQuiz() {
    window.location.hash = `#/surveys/${surveyId}`;
    render(<App />);
    expect(await screen.findByText(/This is a quiz/)).toBeInTheDocument();
    fireEvent.change(screen.getByLabelText(/Capital of France/), { target: { value: "Paris" } });
    fireEvent.click(screen.getByRole("button", { name: "Submit" }));
    expect(await screen.findByText("Thank you!")).toBeInTheDocument();
}

describe("Quizzes", () => {
    beforeEach(() => {
        window.location.hash = "";
        localStorage.clear();
    });

    test("points and scores are written without trailing zeros", () => {
        expect(formatPoints(7.5)).toBe("7.5");
        expect(formatPoints(0.333333)).toBe("0.33");
        expect(formatScore(8, 10)).toBe("8 of 10 points");
        expect(formatScore(1, 1)).toBe("1 of 1 point");
    });

    test("the thank-you screen shows the score and reviews each question", async () => {
        mockApi({ id: "r1", answers: { capital: ["Paris"] }, score: 1.5, maxScore: 3, quiz: graded });
        await submitQuiz();

        expect(screen.getByText("1.5 of 3 points")).toBeInTheDocument();
        expect(screen.getByText("50%")).toBeInTheDocument();
        expect(screen.getByRole("progressbar", { name: "Score: 50%" })).toHaveAttribute("value", "50");

        const items = screen.getAllByRole("listitem");
        expect(items.map((item) => item.getAttribute("data-correct"))).toEqual(["true", "false", "false"]);
        expect(within(items[0]).getByText("Correct")).toBeInTheDocument();
        expect(within(items[0]).getByText("Since the tenth century.")).toBeInTheDocument();
        expect(items[0].querySelector("[data-quiz-correct-answer]")).toBeNull();
        expect(within(items[1]).getByText("Partly correct")).toBeInTheDocument();
        expect(items[1].querySelector("[data-quiz-correct-answer]")).toHaveTextContent("Correct answer: Two, Three");
        expect(items[1].querySelector("[data-quiz-points]")).toHaveTextContent("0.5 / 1");
        expect(within(items[2]).getByText("Incorrect")).toBeInTheDocument();
        expect(items[2]).toHaveTextContent("Your answer: No answer");
    });

    test("a score-only quiz shows the score without a review", async () => {
        mockApi({ id: "r1", quiz: { score: 1, maxScore: 1, percent: 100, questions: [] } });
        await submitQuiz();

        expect(screen.getByText("1 of 1 point")).toBeInTheDocument();
        expect(screen.queryByText("Review your answers")).not.toBeInTheDocument();
    });

    test("a submission without a grade shows the plain thank-you", async () => {
        mockApi({});
        await submitQuiz();

        expect(screen.queryByText("Your score")).not.toBeInTheDocument();
    });

    test("nothing to grade shows no percentage", () => {
        render(<QuizScore result={{ score: 0, maxScore: 0, percent: 0, questions: [] }} />);

        expect(screen.getByText("0 of 0 points")).toBeInTheDocument();
        expect(screen.queryByText("0%")).not.toBeInTheDocument();
        expect(screen.queryByRole("progressbar")).not.toBeInTheDocument();
    });
});
