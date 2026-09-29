import { parseBool, visibleKeys } from "../../FormFlow.React/src/logic/visibility";
import { QuestionDefinition } from "../../FormFlow.React/src/types/QuestionDefinition";
import { Answers } from "../../FormFlow.React/src/types/Survey";

const q = (key: string, visibleIf?: QuestionDefinition["visibleIf"]): QuestionDefinition => ({
  id: key,
  key,
  label: key,
  type: "yes_no",
  visibleIf,
});

describe("visibleKeys", () => {
  test("questions without rules are always visible", () => {
    expect([...visibleKeys([q("a"), q("b")], {})]).toEqual(["a", "b"]);
  });

  test.each([
    ["true", true],
    ["yes", true],
    ["false", false],
    [undefined, false],
  ])("answer %s shows the dependent question: %s", (answer, shown) => {
    const questions = [q("student"), q("campus", { key: "student", shouldEqual: true })];
    const answers: Answers = answer === undefined ? {} : { student: [answer] };

    expect(visibleKeys(questions, answers).has("campus")).toBe(shown);
  });

  test("a hidden controlling question hides its dependents", () => {
    const questions = [
      q("a"),
      q("b", { key: "a", shouldEqual: true }),
      q("c", { key: "b", shouldEqual: true }),
    ];

    expect([...visibleKeys(questions, { a: ["false"], b: ["true"] })]).toEqual(["a"]);
  });

  test("rules pointing outside the survey or in a cycle hide the question", () => {
    const questions = [
      q("orphan", { key: "missing", shouldEqual: true }),
      q("x", { key: "y", shouldEqual: true }),
      q("y", { key: "x", shouldEqual: true }),
    ];

    expect(visibleKeys(questions, { x: ["true"], y: ["true"] }).size).toBe(0);
  });
});

describe("parseBool", () => {
  test("accepts true/false and yes/no in any case", () => {
    expect(parseBool("YES")).toBe(true);
    expect(parseBool("False")).toBe(false);
    expect(parseBool("maybe")).toBeUndefined();
  });
});
