import React, { useId, useState } from "react";
import { QuestionDefinition } from "../types/QuestionDefinition";
import { parseBool } from "../logic/visibility";

export interface QuestionRendererProps {
    question: QuestionDefinition;
    /** The current answer. Omit (with onChange) to let the component manage its own state. */
    value?: string[];
    onChange?: (values: string[]) => void;
    /** A validation message to show under the question. */
    error?: string;
}

function initialValue(question: QuestionDefinition): string[] {
    const raw = question.defaultValue;
    return raw === undefined || raw === null || raw === "" ? [] : [String(raw)];
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
    const usesGroup = type === "yes_no" || type === "radio" || type === "multiselect"
        || (type === "checkbox" && options.length > 0);

    let input: React.ReactNode;
    switch (type) {
        case "number":
        case "text":
            input = (
                <input
                    id={inputId}
                    type={type === "number" ? "number" : "text"}
                    placeholder={question.placeholder}
                    required={question.required}
                    value={single}
                    onChange={(e) => update([e.target.value])}
                    aria-describedby={describedBy}
                    aria-invalid={error ? true : undefined}
                />
            );
            break;
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
