import React, { useState } from "react";
import { fireEvent, render, screen } from "@testing-library/react";
import "@testing-library/jest-dom";
import { SurveyForm } from "../../FormFlow.React/src/components/SurveyForm";
import { QuestionRenderer, ratingScale } from "../../FormFlow.React/src/components/QuestionRenderer";
import { QuestionDefinition } from "../../FormFlow.React/src/types/QuestionDefinition";
import { Answers } from "../../FormFlow.React/src/types/Survey";

const questions: QuestionDefinition[] = [
  { id: "1", key: "is_student", label: "Are you a student?", type: "yes_no", required: true },
  {
    id: "2",
    key: "campus",
    label: "Which campus?",
    type: "dropdown",
    options: [
      { label: "North", value: "north" },
      { label: "South", value: "south" },
    ],
    visibleIf: { key: "is_student", shouldEqual: true },
  },
  {
    id: "3",
    key: "skills",
    label: "Skills",
    type: "multiselect",
    options: [
      { label: "C#", value: "csharp" },
      { label: "SQL", value: "sql" },
    ],
  },
];

function Harness({ onAnswers }: { onAnswers: (a: Answers) => void }) {
  const [answers, setAnswers] = useState<Answers>({});
  return (
    <SurveyForm
      questions={questions}
      answers={answers}
      errors={{ skills: ["Pick at least one skill."] }}
      onChange={(next) => {
        setAnswers(next);
        onAnswers(next);
      }}
    />
  );
}

describe("SurveyForm", () => {
  test("shows the conditional question only after answering yes", () => {
    render(<Harness onAnswers={() => undefined} />);

    expect(screen.queryByLabelText("Which campus?")).not.toBeInTheDocument();

    fireEvent.click(screen.getByLabelText("Yes"));
    expect(screen.getByLabelText("Which campus?")).toBeInTheDocument();

    fireEvent.click(screen.getByLabelText("No"));
    expect(screen.queryByLabelText("Which campus?")).not.toBeInTheDocument();
  });

  test("reports answers for every question type in option order", () => {
    const seen: Answers[] = [];
    render(<Harness onAnswers={(a) => seen.push(a)} />);

    fireEvent.click(screen.getByLabelText("Yes"));
    fireEvent.change(screen.getByLabelText("Which campus?"), { target: { value: "south" } });
    fireEvent.click(screen.getByLabelText("SQL"));
    fireEvent.click(screen.getByLabelText("C#"));

    expect(seen[seen.length - 1]).toEqual({
      is_student: ["true"],
      campus: ["south"],
      skills: ["csharp", "sql"],
    });
  });

  test("shows server validation messages next to the question", () => {
    render(<Harness onAnswers={() => undefined} />);

    expect(screen.getByRole("alert")).toHaveTextContent("Pick at least one skill.");
  });
});

describe("QuestionRenderer types", () => {
  test("a checkbox without options is a single tick box", () => {
    const changes: string[][] = [];
    render(
      <QuestionRenderer
        question={{ id: "c", key: "agree", label: "I agree", type: "checkbox" }}
        value={[]}
        onChange={(v) => changes.push(v)}
      />
    );

    fireEvent.click(screen.getByLabelText("I agree"));

    expect(changes).toEqual([["true"]]);
  });

  test("radio questions report the chosen option", () => {
    const changes: string[][] = [];
    render(
      <QuestionRenderer
        question={{
          id: "r",
          key: "contact",
          label: "Contact",
          type: "radio",
          options: [
            { label: "Email", value: "email" },
            { label: "Phone", value: "phone" },
          ],
        }}
        value={[]}
        onChange={(v) => changes.push(v)}
      />
    );

    fireEvent.click(screen.getByLabelText("Phone"));

    expect(changes).toEqual([["phone"]]);
  });
  test("a rating shows one star per point and reports the one picked", () => {
    const changes: string[][] = [];
    render(
      <QuestionRenderer
        question={{
          id: "s",
          key: "stars",
          label: "Rate it",
          type: "rating",
          validationConfigs: '[{"validationType":"MaxValue","maxValue":7}]',
        }}
        value={["2"]}
        onChange={(v) => changes.push(v)}
      />
    );

    expect(screen.getAllByRole("radio")).toHaveLength(7);
    expect(screen.getByLabelText("2 of 7")).toBeChecked();
    fireEvent.click(screen.getByLabelText("6 of 7"));

    expect(changes).toEqual([["6"]]);
  });

  test("the rating scale defaults to 5 and ignores bad rules", () => {
    const base = { id: "s", key: "s", label: "S", type: "rating" };
    expect(ratingScale(base)).toBe(5);
    expect(ratingScale({ ...base, validationConfigs: '[{"validationType":"Range","minValue":1,"maxValue":3}]' })).toBe(3);
    expect(ratingScale({ ...base, validationConfigs: '[{"validationType":"MaxValue","maxValue":50}]' })).toBe(5);
    expect(ratingScale({ ...base, validationConfigs: "not json" })).toBe(5);
  });

  test.each([
    ["email", "input[type=email]"],
    ["date", "input[type=date]"],
    ["long_text", "textarea"],
  ])("a %s question uses the matching input", (type, selector) => {
    const changes: string[][] = [];
    const { container } = render(
      <QuestionRenderer
        question={{ id: "q", key: "q", label: "Question", type }}
        value={[]}
        onChange={(v) => changes.push(v)}
      />
    );

    const input = container.querySelector(selector) as HTMLInputElement;
    expect(input).not.toBeNull();
    expect(screen.getByLabelText("Question")).toBe(input);
    fireEvent.change(input, { target: { value: type === "date" ? "2025-08-18" : "hello@example.com" } });

    expect(changes).toEqual([[type === "date" ? "2025-08-18" : "hello@example.com"]]);
  });
});
