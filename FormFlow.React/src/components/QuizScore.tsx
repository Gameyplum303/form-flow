import React from "react";
import { QuestionGrade, QuizResult } from "../types/Quiz";

/** Points without trailing zeros, as "7.5" or "2". */
export function formatPoints(points: number): string {
    return String(Math.round(points * 100) / 100);
}

/** A score out of a maximum, as "8 of 10 points" or "1 of 1 point". */
export function formatScore(score: number, max: number): string {
    return `${formatPoints(score)} of ${formatPoints(max)} ${max === 1 ? "point" : "points"}`;
}

function verdict(grade: QuestionGrade): "correct" | "partly" | "incorrect" {
    return grade.correct ? "correct" : grade.points > 0 ? "partly" : "incorrect";
}

const verdictText = { correct: "Correct", partly: "Partly correct", incorrect: "Incorrect" };

/**
 * The thank-you screen of a quiz: the respondent's score, then, when the survey shows them, each
 * graded question marked right or wrong with the correct answer and the explanation.
 */
export function QuizScore({ result }: { result: QuizResult }) {
    const graded = result.maxScore > 0;
    return (
        <section className="quiz-result" aria-labelledby="quiz-score-heading" data-quiz-result>
            <h3 id="quiz-score-heading">Your score</h3>
            <p className="quiz-score">
                <span data-quiz-score>{formatScore(result.score, result.maxScore)}</span>
                {graded && <span className="quiz-percent" data-quiz-percent>{result.percent}%</span>}
            </p>
            {graded && (
                <progress className="quiz-bar" value={result.percent} max={100} aria-label={`Score: ${result.percent}%`} />
            )}
            {result.questions.length > 0 && (
                <>
                    <h3>Review your answers</h3>
                    <ol className="quiz-review" data-quiz-review>
                        {result.questions.map((grade) => {
                            const kind = verdict(grade);
                            return (
                                <li key={grade.key} className={`quiz-question ${kind}`} data-quiz-question={grade.key}
                                    data-correct={grade.correct ? "true" : "false"}>
                                    <div className="quiz-question-head">
                                        <h4>{grade.label}</h4>
                                        <span className="quiz-points" data-quiz-points>
                                            {formatPoints(grade.points)} / {formatPoints(grade.maxPoints)}
                                        </span>
                                    </div>
                                    <p className="quiz-verdict" data-quiz-verdict>{verdictText[kind]}</p>
                                    <p>
                                        <strong>Your answer:</strong>{" "}
                                        {grade.givenAnswers.length > 0 ? grade.givenAnswers.join(", ") : "No answer"}
                                    </p>
                                    {!grade.correct && (
                                        <p data-quiz-correct-answer>
                                            <strong>Correct answer:</strong> {grade.correctAnswers.join(", ")}
                                        </p>
                                    )}
                                    {grade.explanation && <p className="help" data-quiz-explanation>{grade.explanation}</p>}
                                </li>
                            );
                        })}
                    </ol>
                </>
            )}
        </section>
    );
}
