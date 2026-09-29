import React, { useCallback, useEffect, useState } from "react";
import "./App.css";
import {
    API_BASE, getSurvey, getSurveyByShareCode, getSurveyQuestions, getSurveys, hasAnswered, respondentId, submitResponse,
} from "./api";
import { initialValue } from "./components/QuestionRenderer";
import { SurveyForm } from "./components/SurveyForm";
import { clearDraft, loadDraft, saveDraft } from "./logic/drafts";
import { firstPageWithError, missingAnswers, shownPages, splitPages } from "./logic/pages";
import { visibleKeys } from "./logic/visibility";
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
    /** The page last moved to, by its index among all pages. */
    const [page, setPage] = useState(0);
    /** Whether the answers came back from this browser's storage. */
    const [restored, setRestored] = useState(false);
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
                if (isClosed(s) || answered) {
                    setAnswers(defaultAnswers(q));
                    setMessage(isClosed(s) ? CLOSED_MESSAGE : ANSWERED_MESSAGE);
                    setStatus("closed");
                    return;
                }
                // Pick up answers this browser saved earlier, for questions still in the survey.
                const keys = new Set(q.map((question) => question.key));
                const saved = Object.entries(loadDraft(s.id) ?? {}).filter(([key]) => keys.has(key));
                setAnswers({ ...defaultAnswers(q), ...Object.fromEntries(saved) });
                setRestored(saved.length > 0);
                setStatus("ready");
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

    const pages = splitPages(questions, survey?.pageBreaks);
    const paged = pages.length > 1;
    const visible = visibleKeys(questions, answers);
    const shown = shownPages(pages, visible);
    // When answers hide the page last moved to, the next page that shows takes its place.
    const current = shown.includes(page) ? page : shown.find((i) => i > page) ?? shown[shown.length - 1];
    const position = shown.indexOf(current);
    const lastPage = position === shown.length - 1;

    const changeAnswers = (next: Answers) => {
        setAnswers(next);
        if (survey !== null) {
            saveDraft(survey.id, next);
        }
    };

    const goTo = (next: number) => {
        setPage(next);
        try {
            window.scrollTo(0, 0);
        } catch {
            // Not every environment can scroll.
        }
    };

    const startOver = () => {
        if (survey !== null) {
            clearDraft(survey.id);
        }
        setAnswers(defaultAnswers(questions));
        setErrors({});
        setMessage(null);
        setRestored(false);
        setPage(0);
    };

    /** Checks the page's required answers as the API would, then moves on when they're all there. */
    const next = () => {
        const onPage = new Set(pages[current].map((q) => q.key));
        const missing = missingAnswers(pages[current], visible, answers);
        const kept = Object.fromEntries(Object.entries(errors).filter(([key]) => !onPage.has(key)));
        setErrors({ ...kept, ...missing });
        if (Object.keys(missing).length === 0) {
            goTo(shown[position + 1]);
        }
    };

    const submit = async (event: React.FormEvent) => {
        event.preventDefault();
        if (survey === null) {
            return;
        }
        // Enter in a field on an earlier page moves on rather than submitting.
        if (paged && !lastPage) {
            next();
            return;
        }
        setStatus("submitting");
        try {
            const result = await submitResponse(survey.id, answers, respondent);
            if (result.ok) {
                clearDraft(survey.id);
                setStatus("done");
                return;
            }
            setErrors(result.errors);
            setMessage(result.message);
            if (result.final) {
                clearDraft(survey.id);
                setStatus("closed");
                return;
            }
            const withError = firstPageWithError(pages, result.errors);
            if (paged && withError !== undefined) {
                goTo(withError);
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
            {restored && (
                <p className="notice" data-resume-note>
                    We saved your answers on this device.{" "}
                    <button type="button" className="link" onClick={startOver}>Start over</button>
                </p>
            )}
            {message && <p className="notice error" role="alert">{message}</p>}
            {paged && (
                <div className="progress" data-survey-progress>
                    <span className="progress-label" id="survey-progress-label">Page {position + 1} of {shown.length}</span>
                    <progress value={position + 1} max={shown.length} aria-labelledby="survey-progress-label" />
                </div>
            )}
            <SurveyForm questions={questions} answers={answers} errors={errors} onChange={changeAnswers}
                page={paged ? pages[current] : undefined} />
            <div className="survey-nav">
                {paged && position > 0 && (
                    <button type="button" className="secondary" onClick={() => goTo(shown[position - 1])}>Back</button>
                )}
                {paged && !lastPage ? (
                    <button type="submit">Next</button>
                ) : (
                    <button type="submit" disabled={status === "submitting"}>
                        {status === "submitting" ? "Submitting..." : "Submit"}
                    </button>
                )}
            </div>
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
