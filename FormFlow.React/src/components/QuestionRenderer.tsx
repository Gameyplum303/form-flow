import React, { useId, useState } from "react";
import { QuestionDefinition } from "../types/QuestionDefinition";
import { Option } from "../types/Option";
import { parseBool } from "../logic/visibility";

export interface QuestionRendererProps {
    question: QuestionDefinition;
    /** The current answer. Omit (with onChange) to let the component manage its own state. */
    value?: string[];
    onChange?: (values: string[]) => void;
    /** A validation message to show under the question. */
    error?: string;
}

const defaultRatingScale = 5;
const maxRatingScale = 10;

/** A number from the question's MinValue or MaxValue rule (or a Range rule), if it has one. Mirrors ValidationRules. */
function ruleValue(question: QuestionDefinition, limit: "minValue" | "maxValue"): number | undefined {
    try {
        const rules = JSON.parse(question.validationConfigs ?? "[]");
        const type = limit === "minValue" ? "MinValue" : "MaxValue";
        const rule = Array.isArray(rules)
            ? rules.find((r) => (r?.validationType === type || r?.validationType === "Range") && typeof r?.[limit] === "number")
            : undefined;
        return rule?.[limit];
    } catch {
        return undefined;
    }
}

/** Stars in a rating question: its MaxValue rule (2 to 10) when set, otherwise 5. Mirrors QuestionTypes.RatingScale. */
export function ratingScale(question: QuestionDefinition): number {
    const max = Number(ruleValue(question, "maxValue"));
    return Number.isInteger(max) && max >= 2 && max <= maxRatingScale ? max : defaultRatingScale;
}

/** The scale a likert grid uses when it has no options. Mirrors QuestionTypes.DefaultLikertScale. */
export const defaultLikertScale: Option[] = [
    { value: "1", label: "Strongly disagree" },
    { value: "2", label: "Disagree" },
    { value: "3", label: "Neutral" },
    { value: "4", label: "Agree" },
    { value: "5", label: "Strongly agree" },
];

/** The columns of a likert grid: its options, or the default agreement scale. */
export function likertScale(question: QuestionDefinition): Option[] {
    return question.options && question.options.length > 0 ? question.options : defaultLikertScale;
}

/** A slider's lowest and highest values: its MinValue and MaxValue rules, or 0 and 100. Mirrors QuestionTypes.SliderRange. */
export function sliderRange(question: QuestionDefinition): { min: number; max: number } {
    return { min: ruleValue(question, "minValue") ?? 0, max: ruleValue(question, "maxValue") ?? 100 };
}

/** The highest NPS answer; answers run from 0. */
export const npsMax = 10;

/** Reads likert answers ("row=option") into the option picked per row. */
function likertAnswers(values: string[]): Record<string, string> {
    const chosen: Record<string, string> = {};
    values.forEach((v) => {
        const at = v.indexOf("=");
        if (at > 0) {
            chosen[v.slice(0, at)] = v.slice(at + 1);
        }
    });
    return chosen;
}

/** The question's default answer, or no answer when it has none. A likert grid has no default. */
export function initialValue(question: QuestionDefinition): string[] {
    const raw = question.defaultValue;
    return raw === undefined || raw === null || raw === "" || question.type.toLowerCase() === "likert" ? [] : [String(raw)];
}

/**
 * Renders one question with the input that fits its type. It works standalone or as a
 * controlled input inside a form when `value` and `onChange` are passed.
 */
export function QuestionRenderer({ question, value, onChange, error }: QuestionRendererProps) {
    const inputId = `question-${question.key}-${useId().replace(/:/g, "")}`;
    const helpTextId = question.helpText ? `${inputId}-help` : undefined;
    const errorId = error ? `${inputId}-error` : undefined;
    const describedBy = [helpTextId, errorId].filter(Boolean).join(" ") || undefined;

    const [ownValue, setOwnValue] = useState<string[]>(() => initialValue(question));
    const controlled = onChange !== undefined;
    const current = controlled ? value ?? [] : ownValue;
    const single = current[0] ?? "";

    const update = (next: string[]) => {
        const cleaned = next.filter((v) => v !== "");
        if (controlled) {
            onChange!(cleaned);
        } else {
            setOwnValue(cleaned);
        }
    };

    const toggle = (optionValue: string, checked: boolean) => {
        const selected = new Set(current);
        if (checked) {
            selected.add(optionValue);
        } else {
            selected.delete(optionValue);
        }
        // Keep option order so stored answers are stable.
        update((question.options ?? []).map((o) => o.value).filter((v) => selected.has(v)));
    };

    const requiredMark = question.required ? (
        <span className="required" aria-hidden="true">*</span>
    ) : null;

    const type = question.type.toLowerCase();
    const options = question.options ?? [];
    const usesGroup = type === "yes_no" || type === "radio" || type === "multiselect" || type === "rating"
        || type === "likert" || type === "nps" || (type === "checkbox" && options.length > 0);

    let input: React.ReactNode;
    switch (type) {
        case "number":
        case "text":
        case "email":
        case "date":
            input = (
                <input
                    id={inputId}
                    type={type === "text" ? "text" : type}
                    placeholder={question.placeholder}
                    required={question.required}
                    value={single}
                    onChange={(e) => update([e.target.value])}
                    aria-describedby={describedBy}
                    aria-invalid={error ? true : undefined}
                />
            );
            break;
        case "long_text":
            input = (
                <textarea
                    id={inputId}
                    rows={4}
                    placeholder={question.placeholder}
                    required={question.required}
                    value={single}
                    onChange={(e) => update([e.target.value])}
                    aria-describedby={describedBy}
                    aria-invalid={error ? true : undefined}
                />
            );
            break;
        case "rating": {
            const stars = Array.from({ length: ratingScale(question) }, (_, i) => i + 1);
            const chosen = Number(single) || 0;
            input = (
                <div className="rating">
                    {stars.map((n) => (
                        <label key={n} className={`star${n <= chosen ? " filled" : ""}`}
                            title={`${n} of ${stars.length}`}>
                            <input type="radio" name={inputId} value={String(n)} checked={chosen === n}
                                onChange={() => update([String(n)])}
                                aria-label={`${n} of ${stars.length}`} />
                            <span aria-hidden="true">★</span>
                        </label>
                    ))}
                </div>
            );
            break;
        }
        case "likert": {
            // Each statement is a radio group labelled by the statement; each radio button carries its
            // column's label, so the visible headings are hidden from screen readers.
            const scale = likertScale(question);
            const rows = question.rows ?? [];
            const chosen = likertAnswers(current);
            const pick = (row: string, option: string) => {
                const next = { ...chosen, [row]: option };
                update(rows.filter((r) => next[r.value] !== undefined).map((r) => `${r.value}=${next[r.value]}`));
            };
            input = (
                <div className="likert-scroll">
                    <div className="likert-grid">
                        <div className="likert-row likert-head" aria-hidden="true">
                            <div className="likert-statement" />
                            {scale.map((o) => (
                                <div key={o.value} className="likert-column">{o.label}</div>
                            ))}
                        </div>
                        {rows.map((row, i) => {
                            const statementId = `${inputId}-row-${i}`;
                            return (
                                <div key={row.value} className="likert-row" role="radiogroup" aria-labelledby={statementId}>
                                    <div id={statementId} className="likert-statement">{row.label}</div>
                                    {scale.map((o) => (
                                        <label key={o.value} className="likert-column">
                                            <input type="radio" name={statementId} value={o.value} aria-label={o.label}
                                                checked={chosen[row.value] === o.value}
                                                onChange={() => pick(row.value, o.value)} />
                                        </label>
                                    ))}
                                </div>
                            );
                        })}
                    </div>
                </div>
            );
            break;
        }
        case "nps": {
            // Eleven buttons from 0 to 10; pressing the chosen one again clears the answer.
            const scores = Array.from({ length: npsMax + 1 }, (_, i) => String(i));
            input = (
                <>
                    <div className="nps-scale">
                        {scores.map((n) => (
                            <button key={n} type="button" className={`nps-button${single === n ? " selected" : ""}`}
                                aria-pressed={single === n}
                                aria-label={n === "0" ? "0, not likely" : n === String(npsMax) ? `${npsMax}, extremely likely` : undefined}
                                onClick={() => update(single === n ? [] : [n])}>
                                {n}
                            </button>
                        ))}
                    </div>
                    <div className="scale-ends" aria-hidden="true">
                        <span>Not likely</span>
                        <span>Extremely likely</span>
                    </div>
                </>
            );
            break;
        }
        case "slider": {
            // Unanswered, with the handle at the minimum and no value shown, until it is moved.
            const { min, max } = sliderRange(question);
            input = (
                <div className="slider-field">
                    <div className="slider-track">
                        <input
                            id={inputId}
                            type="range"
                            className={single === "" ? "unanswered" : undefined}
                            min={min}
                            max={max}
                            step={1}
                            value={single === "" ? min : single}
                            onChange={(e) => update([e.target.value])}
                            aria-valuetext={single === "" ? "Not answered" : undefined}
                            aria-describedby={describedBy}
                            aria-invalid={error ? true : undefined}
                        />
                        <div className="scale-ends" aria-hidden="true">
                            <span>{min}</span>
                            <span>{single === "" ? "Move the slider to answer" : ""}</span>
                            <span>{max}</span>
                        </div>
                    </div>
                    <span className="slider-value" aria-hidden="true">{single === "" ? "–" : single}</span>
                </div>
            );
            break;
        }
        case "dropdown":
            input = (
                <select
                    id={inputId}
                    required={question.required}
                    value={single}
                    onChange={(e) => update([e.target.value])}
                    aria-describedby={describedBy}
                    aria-invalid={error ? true : undefined}
                >
                    <option value="">{question.placeholder ?? "Select..."}</option>
                    {options.map((o) => (
                        <option key={o.value} value={o.value}>{o.label}</option>
                    ))}
                </select>
            );
            break;
        case "yes_no": {
            const answer = parseBool(single);
            input = [
                { label: "Yes", value: "true", checked: answer === true },
                { label: "No", value: "false", checked: answer === false },
            ].map((o) => (
                <label key={o.value} className="choice">
                    <input type="radio" name={inputId} value={o.value} checked={o.checked}
                        onChange={() => update([o.value])} />
                    {o.label}
                </label>
            ));
            break;
        }
        case "radio":
            input = options.map((o) => (
                <label key={o.value} className="choice">
                    <input type="radio" name={inputId} value={o.value} checked={single === o.value}
                        onChange={() => update([o.value])} />
                    {o.label}
                </label>
            ));
            break;
        case "checkbox":
            if (options.length === 0) {
                // A checkbox without options is a single tick box.
                input = (
                    <input id={inputId} type="checkbox" checked={parseBool(single) === true}
                        onChange={(e) => update([e.target.checked ? "true" : "false"])}
                        aria-describedby={describedBy} />
                );
                break;
            }
            input = options.map((o) => (
                <label key={o.value} className="choice">
                    <input type="checkbox" value={o.value} checked={current.includes(o.value)}
                        onChange={(e) => toggle(o.value, e.target.checked)} />
                    {o.label}
                </label>
            ));
            break;
        case "multiselect":
            input = options.map((o) => (
                <label key={o.value} className="choice">
                    <input type="checkbox" value={o.value} checked={current.includes(o.value)}
                        onChange={(e) => toggle(o.value, e.target.checked)} />
                    {o.label}
                </label>
            ));
            break;
        default:
            input = <p className="error">Unsupported question type: {question.type}</p>;
    }

    const footer = (
        <>
            {question.helpText && (
                <small id={helpTextId} className="help">{question.helpText}</small>
            )}
            {error && (
                <small id={errorId} className="error" role="alert">{error}</small>
            )}
        </>
    );

    if (usesGroup) {
        return (
            <fieldset className={`question${error ? " has-error" : ""}`} aria-describedby={describedBy}>
                <legend>{question.label}{requiredMark}</legend>
                <div className="choices">{input}</div>
                {footer}
            </fieldset>
        );
    }

    return (
        <div className={`question${error ? " has-error" : ""}`}>
            <label htmlFor={inputId} className="question-label">
                {question.label}{requiredMark}
            </label>
            {input}
            {footer}
        </div>
    );
}
