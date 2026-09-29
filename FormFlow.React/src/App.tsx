import React, { useCallback, useEffect, useState } from "react";
import "./App.css";
import { API_BASE, getSurvey, getSurveyQuestions, getSurveys, submitResponse } from "./api";
import { SurveyForm } from "./components/SurveyForm";
import { QuestionDefinition } from "./types/QuestionDefinition";
import { AnswerErrors, Answers, SurveyDefinition } from "./types/Survey";

/** The survey id in the URL hash (#/surveys/<id>), if any. */
function surveyIdFromHash(): string | undefined {
    const match = window.location.hash.match(/^#\/surveys\/([0-9a-fA-F-]{36})$/);
    return match?.[1];
}

function defaultAnswers(questions: QuestionDefinition[]): Answers {
    const answers: Answers = {};
    questions.forEach((q) => {
        if (q.defaultValue !== undefined && q.defaultValue !== null && q.defaultValue !== "") {
            answers[q.key] = [String(q.defaultValue)];
        }
    });
    return answers;
}

function SurveyList({ onOpen }: { onOpen: (id: string) => void }) {
    const [surveys, setSurveys] = useState<SurveyDefinition[] | null>(null);
    const [failed, setFailed] = useState(false);

    useEffect(() => {
        getSurveys().then(setSurveys).catch(() => setFailed(true));
    }, []);

    if (failed) {
        return (
            <p className="notice error">
                Could not reach the API at {API_BASE}. Start FormFlow.Backend, or set REACT_APP_API_URL.
            </p>
        );
    }
    if (surveys === null) {
        return <p className="notice">Loading surveys...</p>;
    }
    if (surveys.length === 0) {
        return <p className="notice">There are no surveys to take yet.</p>;
    }

    return (
        <ul className="survey-list">
            {surveys.map((s) => (
                <li key={s.id} className="card">
                    <h2>{s.title}</h2>
                    <p>{s.description}</p>
                    <button type="button" onClick={() => onOpen(s.id)}>Take survey</button>
                </li>
            ))}
        </ul>
    );
}

function TakeSurvey({ surveyId, onBack }: { surveyId: string; onBack: () => void }) {
    const [survey, setSurvey] = useState<SurveyDefinition | null>(null);
    const [questions, setQuestions] = useState<QuestionDefinition[]>([]);
    const [answers, setAnswers] = useState<Answers>({});
    const [errors, setErrors] = useState<AnswerErrors>({});
    const [message, setMessage] = useState<string | null>(null);
    const [status, setStatus] = useState<"loading" | "missing" | "ready" | "submitting" | "done">("loading");

    useEffect(() => {
        Promise.all([getSurvey(surveyId), getSurveyQuestions(surveyId)])
            .then(([s, q]) => {
                setSurvey(s);
                setQuestions(q);
                setAnswers(defaultAnswers(q));
                setStatus("ready");
            })
            .catch(() => setStatus("missing"));
    }, [surveyId]);

    const submit = async (event: React.FormEvent) => {
        event.preventDefault();
        setStatus("submitting");
        try {
            const result = await submitResponse(surveyId, answers);
            if (result.ok) {
                setStatus("done");
                return;
            }
            setErrors(result.errors);
            setMessage(result.message);
        } catch {
            setMessage("Could not reach the server. Please try again.");
        }
        setStatus("ready");
    };

    if (status === "loading") {
        return <p className="notice">Loading survey...</p>;
    }
    if (status === "missing" || survey === null) {
        return (
            <>
                <p className="notice error">This survey does not exist or is no longer available.</p>
                <button type="button" className="link" onClick={onBack}>Back to surveys</button>
            </>
        );
    }
    if (status === "done") {
        return (
            <div className="card">
                <h2>Thank you!</h2>
                <p>Your answers to "{survey.title}" were recorded.</p>
                <button type="button" onClick={onBack}>Back to surveys</button>
            </div>
        );
    }

    return (
        <form onSubmit={submit} noValidate>
            <button type="button" className="link" onClick={onBack}>&larr; All surveys</button>
            <h2>{survey.title}</h2>
            <p>{survey.description}</p>
            {message && <p className="notice error" role="alert">{message}</p>}
            <SurveyForm questions={questions} answers={answers} errors={errors} onChange={setAnswers} />
            <button type="submit" disabled={status === "submitting"}>
                {status === "submitting" ? "Submitting..." : "Submit"}
            </button>
        </form>
    );
}

function App() {
    const [surveyId, setSurveyId] = useState<string | undefined>(surveyIdFromHash);

    useEffect(() => {
        const onHashChange = () => setSurveyId(surveyIdFromHash());
        window.addEventListener("hashchange", onHashChange);
        return () => window.removeEventListener("hashchange", onHashChange);
    }, []);

    const open = useCallback((id: string) => {
        window.location.hash = `#/surveys/${id}`;
        setSurveyId(id);
    }, []);

    const back = useCallback(() => {
        window.location.hash = "";
        setSurveyId(undefined);
    }, []);

    return (
        <main className="app">
            <header>
                <h1>FormFlow</h1>
                <p>Surveys rendered from question definitions stored by the FormFlow API.</p>
            </header>
            {surveyId ? <TakeSurvey surveyId={surveyId} onBack={back} /> : <SurveyList onOpen={open} />}
        </main>
    );
}

export default App;
