import { firstPageWithError, missingAnswers, shownPages, splitPages } from "../../FormFlow.React/src/logic/pages";
import { visibleKeys } from "../../FormFlow.React/src/logic/visibility";
import { QuestionDefinition } from "../../FormFlow.React/src/types/QuestionDefinition";

const q = (id: string, key: string, extra: Partial<QuestionDefinition> = {}): QuestionDefinition =>
  ({ id, key, label: key, type: "text", required: true, ...extra } as QuestionDefinition);

const consent = q("1", "consent", { type: "yes_no" });
const name = q("2", "name", { visibleIf: { key: "consent", shouldEqual: true } });
const email = q("3", "email", { visibleIf: { key: "consent", shouldEqual: true } });
const comments = q("4", "comments", { required: false });
const all = [consent, name, email, comments];

describe("splitPages", () => {
  test("is one page without page breaks", () => {
    expect(splitPages(all, undefined)).toEqual([all]);
    expect(splitPages(all, [])).toEqual([all]);
  });

  test("starts a page at each break, ignoring one on the first question or unknown ids", () => {
    expect(splitPages(all, ["1", "2", "4", "nope"]).map((p) => p.map((x) => x.key)))
      .toEqual([["consent"], ["name", "email"], ["comments"]]);
  });

  test("is one empty page without questions", () => {
    expect(splitPages([], ["1"])).toEqual([[]]);
  });
});

describe("shownPages", () => {
  const pages = splitPages(all, ["2", "4"]);

  test("skips a page whose questions are all hidden", () => {
    expect(shownPages(pages, visibleKeys(all, { consent: ["false"] }))).toEqual([0, 2]);
    expect(shownPages(pages, visibleKeys(all, { consent: ["true"] }))).toEqual([0, 1, 2]);
  });

  test("is never empty", () => {
    expect(shownPages([[name]], new Set())).toEqual([0]);
  });
});

describe("missingAnswers", () => {
  test("names visible required questions without an answer, with the API's message", () => {
    const visible = visibleKeys(all, { consent: ["true"] });

    expect(missingAnswers([name, email, comments], visible, { consent: ["true"], name: ["Ada"], email: ["  "] }))
      .toEqual({ email: ["This question is required."] });
  });

  test("ignores hidden questions", () => {
    const visible = visibleKeys(all, { consent: ["false"] });

    expect(missingAnswers([name, email], visible, { consent: ["false"] })).toEqual({});
  });
});

describe("firstPageWithError", () => {
  const pages = splitPages(all, ["2", "4"]);

  test("finds the page of the first error", () => {
    expect(firstPageWithError(pages, { comments: ["Too long"], email: ["Not an email"] })).toBe(1);
  });

  test("is undefined when no error belongs to a page", () => {
    expect(firstPageWithError(pages, { other: ["?"] })).toBeUndefined();
    expect(firstPageWithError(pages, {})).toBeUndefined();
  });
});
