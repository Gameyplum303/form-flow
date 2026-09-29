import React from "react";
import { QuestionDefinition } from "../types/QuestionDefinition";
import { AnswerErrors, Answers } from "../types/Survey";
import { visibleKeys } from "../logic/visibility";
import { QuestionRenderer } from "./QuestionRenderer";

export interface SurveyFormProps {
    questions: QuestionDefinition[];
    answers: Answers;
    errors?: AnswerErrors;
    onChange: (answers: Answers) => void;
}

/** Renders a survey's visible questions and reports every answer change. */
export function SurveyForm({ questions, answers, errors, onChange }: SurveyFormProps) {
    const visible = visibleKeys(questions, answers);

    return (
        <>
            {questions
                .filter((q) => visible.has(q.key))
                .map((q) => (
                    <QuestionRenderer
                        key={q.id}
                        question={q}
                        value={answers[q.key] ?? []}
                        onChange={(values) => onChange({ ...answers, [q.key]: values })}
                        error={errors?.[q.key]?.join(" ")}
                    />
                ))}
        </>
    );
}
