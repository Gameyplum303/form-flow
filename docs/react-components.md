# React Client

`FormFlow.React` is a React 19 + TypeScript app for respondents. It lists the surveys from the API, renders the chosen survey with a control for each question type, shows and hides conditional questions as answers change, and submits to the API. It has no admin features; those are in the Blazor app.

```bash
cd FormFlow.React
npm install
npm start        # http://localhost:3000
```

The app calls `http://localhost:5164` unless `REACT_APP_API_URL` is set.

## Structure

| File | Purpose |
|---|---|
| `src/App.tsx` | Survey list, take-survey screen, thank-you screen, and an "API unreachable" notice. The open survey is kept in the URL hash (`#/surveys/<id>`, or `#/s/<code>` for a share link) so the back button and links work without a router library. A closed survey, or one this browser already answered, shows a message instead of the form. A survey with page breaks shows one page at a time with a progress bar, **Back** and **Next**, and saved answers come back with a **Start over** link. |
| `src/api.ts` | `getSurveys`, `getSurvey`, `getSurveyByShareCode`, `getSurveyQuestions`, `hasAnswered` and `submitResponse`. `respondentId()` keeps this browser's random respondent id in `localStorage`. `submitResponse` returns `{ ok: true }` or `{ ok: false, errors, message, final? }`, with `errors` taken from the API's problem details and `final` set when the survey closed or was already answered. |
| `src/components/SurveyForm.tsx` | Renders the visible questions and passes each one its value and error. |
| `src/components/QuestionRenderer.tsx` | Renders one question with the right control. |
| `src/logic/pages.ts` | `splitPages`, `shownPages`, `missingAnswers` and `firstPageWithError`, which split a survey at its `pageBreaks`, skip pages whose questions are all hidden, check a page's required answers before **Next**, and find the page to open after a server error. |
| `src/logic/drafts.ts` | `loadDraft`, `saveDraft` and `clearDraft` keep a survey's unsent answers in `localStorage` (`formflow.answers.<survey id>`), so they come back after a reload. Storage that is blocked or holds bad data is ignored. |
| `src/logic/visibility.ts` | `visibleKeys(questions, answers)`, the TypeScript twin of the C# `VisibilityEvaluator`, and `parseBool`. |
| `src/types/` | `QuestionDefinition`, `Option`, `VisibleIf`, `SurveyDefinition`, `Answers` and `AnswerErrors`, matching the API's JSON. |

## `QuestionRenderer`

```tsx
<QuestionRenderer question={question} value={answers[question.key]} onChange={v => ...} error={errors[question.key]?.[0]} />
```

| Prop | Type | Purpose |
|---|---|---|
| `question` | `QuestionDefinition` | Required. What to render. |
| `value` | `string[]` | The current answer. Leave out `value` and `onChange` to let the component keep its own state. |
| `onChange` | `(values: string[]) => void` | Called with the new answer. |
| `error` | `string` | Message shown under the question. |

| Type | Control |
|---|---|
| `text` | `<input type="text">` |
| `number` | `<input type="number">` |
| `yes_no` | Yes and No radio buttons |
| `dropdown` | `<select>` |
| `radio` | Radio buttons |
| `checkbox` | One checkbox, or one per option |
| `multiselect` | A checkbox per option |
| `long_text` | `<textarea>` |
| `email` | `<input type="email">` |
| `date` | `<input type="date">` |
| `rating` | A radio button per star, drawn as stars |

Groups of radio buttons and checkboxes are wrapped in `<fieldset>` with a `<legend>`, labels are tied to inputs with ids from `useId`, and errors are linked with `aria-describedby` and `aria-invalid`, so the form works with screen readers.

## `SurveyForm`

| Prop | Type | Purpose |
|---|---|---|
| `questions` | `QuestionDefinition[]` | The survey's questions in order |
| `answers` | `Answers` | Answers keyed by question key |
| `errors` | `AnswerErrors` | Errors keyed by question key |
| `onChange` | `(answers: Answers) => void` | Called with the new answers after any change |
| `page` | `QuestionDefinition[]?` | When set, only these questions are drawn (one page of a paged survey) |

Only questions in `visibleKeys(questions, answers)` are rendered. Answers to hidden questions stay in state, and the API drops them on submit.

## Tests

Tests live in the separate `FormFlow.React.Tests` project. See [testing.md](testing.md#how-the-react-tests-work).
