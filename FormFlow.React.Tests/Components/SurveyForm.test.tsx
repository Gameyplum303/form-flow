import React, { useState } from "react";
import { fireEvent, render, screen } from "@testing-library/react";
import "@testing-library/jest-dom";
import { SurveyForm } from "../../FormFlow.React/src/components/SurveyForm";
import { QuestionRenderer } from "../../FormFlow.React/src/components/QuestionRenderer";
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
});
