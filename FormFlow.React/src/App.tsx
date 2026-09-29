import React, { useCallback, useEffect, useState } from "react";
import "./App.css";
import {
    API_BASE, getSurvey, getSurveyByShareCode, getSurveyQuestions, getSurveys, hasAnswered, respondentId, submitResponse,
} from "./api";
import { initialValue } from "./components/QuestionRenderer";
import { SurveyForm } from "./components/SurveyForm";
import { QuestionDefinition } from "./types/QuestionDefinition";
import { AnswerErrors, Answers, SurveyDefinition } from "./types/Survey";

/** Which survey to open: by id from the list (#/surveys/<id>) or by a share link (#/s/<code>). */
type SurveyTarget = { id: string } | { code: string };

function targetFromHash(): SurveyTarget | undefined {
    const byId = window.location.hash.match(/^#\/surveys\/([0-9a-fA-F-]{36})$/);
    if (byId) {
        return { id: byId[1] };
    }
    const byCode = window.location.hash.match(/^#\/s\/([0-9A-Za-z]{1,32})$/);
    return byCode ? { code: byCode[1] } : undefined;
}

function loadSurvey(target: SurveyTarget): Promise<SurveyDefinition> {
    return "id" in target ? getSurvey(target.id) : getSurveyByShareCode(target.code);
}

/** Matches the API's messages, so a closed survey reads the same before and after submitting. */
const CLOSED_MESSAGE = "This survey is closed and no longer takes answers.";
const ANSWERED_MESSAGE = "You've already answered this survey. Thank you!";

function isClosed(survey: SurveyDefinition): boolean {
    return !!survey.closesAt && new Date(survey.closesAt).getTime() <= Date.now();
}

function defaultAnswers(questions: QuestionDefinition[]): Answers {
    const answers: Answers = {};
    questions.forEach((q) => {
        const initial = initialValue(q);
        if (initial.length > 0) {
            answers[q.key] = initial;
        }
    });
    return answers;
}

function SurveyList({ onOpen }: { onOpen: (id: string) => void }) {
    const [surveys, setSurveys] = useState<SurveyDefinition[] | null>(null);
    const [failed, setFailed] = useState(false);

    useEffect(() => {
        let active = true;
        getSurveys()
            .then((list) => active && setSurveys(list))
            .catch(() => active && setFailed(true));
        return () => {
            active = false;
        };
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

function TakeSurvey({ target, onBack }: { target: SurveyTarget; onBack: () => void }) {
    const [survey, setSurvey] = useState<SurveyDefinition | null>(null);
    const [questions, setQuestions] = useState<QuestionDefinition[]>([]);
    const [answers, setAnswers] = useState<Answers>({});
    const [errors, setErrors] = useState<AnswerErrors>({});
    const [message, setMessage] = useState<string | null>(null);
    const [status, setStatus] = useState<"loading" | "missing" | "ready" | "submitting" | "done" | "closed">("loading");
    const [respondent] = useState(respondentId);
    const targetKey = "id" in target ? `id:${target.id}` : `code:${target.code}`;

    useEffect(() => {
        let active = true;
        (async () => {
            try {
                const s = await loadSurvey(target);
                const [q, answered] = await Promise.all([getSurveyQuestions(s.id), hasAnswered(s.id, respondent)]);
                if (!active) {
                    return;
                }
                setSurvey(s);
                setQuestions(q);
                setAnswers(defaultAnswers(q));
                if (isClosed(s) || answered) {
                    setMessage(isClosed(s) ? CLOSED_MESSAGE : ANSWERED_MESSAGE);
                    setStatus("closed");
                } else {
                    setStatus("ready");
                }
            } catch {
                if (active) {
                    setStatus("missing");
                }
            }
        })();
        return () => {
            active = false;
        };
        // The target object changes identity on every hash change; its key is what matters.
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [targetKey, respondent]);

    const submit = async (event: React.FormEvent) => {
        event.preventDefault();
        if (survey === null) {
            return;
        }
        setStatus("submitting");
        try {
            const result = await submitResponse(survey.id, answers, respondent);
            if (result.ok) {
                setStatus("done");
                return;
            }
            setErrors(result.errors);
            setMessage(result.message);
            if (result.final) {
                setStatus("closed");
                return;
            }
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
    if (status === "closed") {
        return (
            <div className="card" data-survey-closed>
                <h2>{survey.title}</h2>
                <p>{message}</p>
                <button type="button" onClick={onBack}>Back to surveys</button>
            </div>
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
    const [target, setTarget] = useState<SurveyTarget | undefined>(targetFromHash);

    useEffect(() => {
        const onHashChange = () => setTarget(targetFromHash());
        window.addEventListener("hashchange", onHashChange);
        return () => window.removeEventListener("hashchange", onHashChange);
    }, []);

    const open = useCallback((id: string) => {
        window.location.hash = `#/surveys/${id}`;
        setTarget({ id });
    }, []);

    const back = useCallback(() => {
        window.location.hash = "";
        setTarget(undefined);
    }, []);

    return (
        <main className="app">
            <header>
                <h1>FormFlow</h1>
                <p>Surveys rendered from question definitions stored by the FormFlow API.</p>
            </header>
            {/* The key gives each survey a fresh form, so errors from one survey never show on the next. */}
            {target ? (
                <TakeSurvey key={"id" in target ? `id:${target.id}` : `code:${target.code}`} target={target} onBack={back} />
            ) : (
                <SurveyList onOpen={open} />
            )}
        </main>
    );
}

export default App;
