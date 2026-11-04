import React, { useState } from "react";
import { getSurveyByShareCode } from "../api";
import { looksLikeShareCode, normalizeShareCode, SHARE_CODE_LENGTH } from "../logic/shareCodes";

/**
 * Where a student types the code their professor shows. Capitals, spaces and dashes don't matter,
 * and a pasted share link works too. Opens the survey once the API knows the code.
 */
export function JoinByCode({ onJoin, onBack }: { onJoin: (code: string) => void; onBack: () => void }) {
    const [typed, setTyped] = useState("");
    const [error, setError] = useState<string | null>(null);
    const [busy, setBusy] = useState(false);

    const join = async (event: React.FormEvent) => {
        event.preventDefault();
        if (!looksLikeShareCode(typed)) {
            setError(typed.trim() === ""
                ? "Enter the survey code."
                : `That doesn't look like a survey code. Codes have ${SHARE_CODE_LENGTH} letters and numbers.`);
            return;
        }
        const code = normalizeShareCode(typed);
        setBusy(true);
        setError(null);
        try {
            await getSurveyByShareCode(code);
            onJoin(code);
        } catch {
            setError("No open survey has that code. Check it and try again.");
            setBusy(false);
        }
    };

    return (
        <form className="card join" onSubmit={join} noValidate data-join>
            <h2>Join a survey</h2>
            <p>Enter the code you were given, such as <strong>K7M2 P9QA</strong>.</p>
            <label className="question-label" htmlFor="join-code">Survey code</label>
            <input
                id="join-code"
                className="join-code"
                type="text"
                value={typed}
                maxLength={64}
                onChange={(e) => {
                    setTyped(e.target.value);
                    setError(null);
                }}
                autoComplete="off"
                autoCapitalize="characters"
                autoCorrect="off"
                spellCheck={false}
                enterKeyHint="go"
                aria-invalid={error !== null}
                aria-describedby={error ? "join-error" : "join-hint"}
            />
            {error
                ? <p className="error" id="join-error" role="alert">{error}</p>
                : <p className="help" id="join-hint">Codes have {SHARE_CODE_LENGTH} letters and numbers. Capitals and spaces don't matter.</p>}
            <div className="survey-nav">
                <button type="submit" disabled={busy}>{busy ? "Joining..." : "Join"}</button>
                <button type="button" className="link" onClick={onBack}>Back to surveys</button>
            </div>
        </form>
    );
}
