import React from "react";
import { fireEvent, render, screen, within } from "@testing-library/react";
import "@testing-library/jest-dom";
import {
  QuestionRenderer,
  initialValue,
  likertScale,
  sliderRange,
} from "../../FormFlow.React/src/components/QuestionRenderer";
import { QuestionDefinition } from "../../FormFlow.React/src/types/QuestionDefinition";

const grid: QuestionDefinition = {
  id: "g",
  key: "services",
  label: "How much do you agree?",
  type: "likert",
  rows: [
    { label: "The library has what I need", value: "library" },
    { label: "The labs are up to date", value: "labs" },
  ],
};

function renderBound(question: QuestionDefinition, value: string[] = []) {
  const changes: string[][] = [];
  render(<QuestionRenderer question={question} value={value} onChange={(v) => changes.push(v)} />);
  return changes;
}

describe("Likert grid", () => {
  test("each statement is a radio group labelled by it, with a radio button per point of the scale", () => {
    renderBound(grid);

    const groups = screen.getAllByRole("radiogroup");
    expect(groups).toHaveLength(2);
    const library = screen.getByRole("radiogroup", { name: "The library has what I need" });
    expect(within(library).getAllByRole("radio").map((r) => r.getAttribute("aria-label"))).toEqual([
      "Strongly disagree", "Disagree", "Neutral", "Agree", "Strongly agree",
    ]);
    expect(screen.getByRole("group", { name: "How much do you agree?" })).toBeInTheDocument();
  });

  test("shows the bound answers and reports one entry per answered row, in row order", () => {
    const changes = renderBound(grid, ["labs=2"]);

    const labs = screen.getByRole("radiogroup", { name: "The labs are up to date" });
    expect(within(labs).getByLabelText("Disagree")).toBeChecked();

    const library = screen.getByRole("radiogroup", { name: "The library has what I need" });
    fireEvent.click(within(library).getByLabelText("Strongly agree"));

    expect(changes).toEqual([["library=5", "labs=2"]]);
  });

  test("uses the question's options as the scale when it has them", () => {
    const question = { ...grid, options: [{ label: "Never", value: "never" }, { label: "Often", value: "often" }] };
    expect(likertScale(question).map((o) => o.value)).toEqual(["never", "often"]);
    expect(likertScale(grid)).toHaveLength(5);

    const changes = renderBound(question);
    fireEvent.click(within(screen.getByRole("radiogroup", { name: "The labs are up to date" })).getByLabelText("Often"));

    expect(changes).toEqual([["labs=often"]]);
  });

  test("never starts from a default value", () => {
    expect(initialValue({ ...grid, defaultValue: "3" })).toEqual([]);
  });
});

describe("NPS", () => {
  const nps: QuestionDefinition = { id: "n", key: "recommend", label: "Would you recommend us?", type: "nps" };

  test("shows eleven buttons from 0 to 10 with labelled ends, and reports the one pressed", () => {
    const changes = renderBound(nps);

    const group = screen.getByRole("group", { name: "Would you recommend us?" });
    const buttons = within(group).getAllByRole("button");
    expect(buttons.map((b) => b.textContent)).toEqual(["0", "1", "2", "3", "4", "5", "6", "7", "8", "9", "10"]);
    expect(screen.getByRole("button", { name: "0, not likely" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "10, extremely likely" })).toBeInTheDocument();
    expect(screen.getByText("Not likely")).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "8" }));

    expect(changes).toEqual([["8"]]);
  });

  test("marks the chosen score pressed, and pressing it again clears the answer", () => {
    const changes = renderBound(nps, ["9"]);

    expect(screen.getByRole("button", { name: "9" })).toHaveAttribute("aria-pressed", "true");
    expect(screen.getByRole("button", { name: "8" })).toHaveAttribute("aria-pressed", "false");
    fireEvent.click(screen.getByRole("button", { name: "9" }));

    expect(changes).toEqual([[]]);
  });
});

describe("Slider", () => {
  const slider: QuestionDefinition = { id: "s", key: "hours", label: "Hours of study", type: "slider" };

  test("is unanswered until it is moved", () => {
    const changes = renderBound(slider);

    const input = screen.getByLabelText("Hours of study");
    expect(input).toHaveAttribute("type", "range");
    expect(input).toHaveAttribute("min", "0");
    expect(input).toHaveAttribute("max", "100");
    expect(input).toHaveAttribute("aria-valuetext", "Not answered");
    expect(screen.getByText("Move the slider to answer")).toBeInTheDocument();

    fireEvent.change(input, { target: { value: "35" } });

    expect(changes).toEqual([["35"]]);
  });

  test("uses the question's range and shows the current value", () => {
    const question = {
      ...slider,
      validationConfigs: '[{"validationType":"MinValue","minValue":1},{"validationType":"MaxValue","maxValue":7}]',
    };
    expect(sliderRange(question)).toEqual({ min: 1, max: 7 });
    expect(sliderRange({ ...slider, validationConfigs: '[{"validationType":"Range","minValue":-5,"maxValue":5}]' }))
      .toEqual({ min: -5, max: 5 });
    expect(sliderRange({ ...slider, validationConfigs: "not json" })).toEqual({ min: 0, max: 100 });

    renderBound(question, ["4"]);

    expect(screen.getByLabelText("Hours of study")).toHaveValue("4");
    expect(screen.getByText("4")).toHaveClass("slider-value");
    expect(screen.queryByText("Move the slider to answer")).not.toBeInTheDocument();
  });
});
