import React, { useCallback, useEffect, useState } from "react";
import { getRespondentResults } from "../api";
import { RespondentResults } from "../types/RespondentResults";

/**
 * How everyone has answered so far, shown after answering a survey whose owner shares its results.
 * Shows nothing when the survey keeps its results private or has no questions to sum up.
 */
export function ResultsSummary({ surveyId }: { surveyId: string }) {
    const [summary, setSummary] = useState<RespondentResults | null>(null);
    const [loading, setLoading] = useState(false);

    const load = useCallback(async (active: () => boolean = () => true) => {
        setLoading(true);
        const latest = await getRespondentResults(surveyId);
        if (active()) {
            // Keeps the last summary when a refresh fails.
            setSummary((current) => latest ?? current);
            setLoading(false);
        }
    }, [surveyId]);

    useEffect(() => {
        let active = true;
        load(() => active);
        return () => {
            active = false;
        };
    }, [load]);

    if (summary === null || summary.questions.length === 0) {
        return null;
    }

    return (
        <section className="results-summary" data-respondent-results aria-labelledby="results-summary-title">
            <h3 id="results-summary-title">Results so far</h3>
            <p className="results-total" data-respondent-total>
                Based on {summary.totalResponses} {summary.totalResponses === 1 ? "response" : "responses"}
            </p>
            {summary.questions.map((question) => (
                <div key={question.key} className="results-question" data-respondent-result={question.key}>
                    <h4>{question.label}</h4>
                    {question.multipleAnswers && <p className="help">People could pick more than one.</p>}
                    {question.options.map((option) => (
                        <div key={option.value} className="result-row" data-option-bar={option.label}>
                            <span className="result-label">{option.label}</span>
                            <span className="result-bar" role="img" aria-label={`${option.label}: ${option.count} (${option.percent}%)`}>
                                <span className="result-fill" style={{ width: `${option.percent}%` }} />
                            </span>
                            <span className="result-count">{option.count} ({option.percent}%)</span>
                        </div>
                    ))}
                </div>
            ))}
            <button type="button" className="link" onClick={() => load()} disabled={loading}>Show the latest</button>
        </section>
    );
}
