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
| `src/App.tsx` | Survey list, take-survey screen, thank-you screen, and an "API unreachable" notice. The open survey is kept in the URL hash (`#/surveys/<id>`) so the back button and links work without a router library. |
| `src/api.ts` | `getSurveys`, `getSurvey`, `getSurveyQuestions` and `submitResponse`. `submitResponse` returns `{ ok: true }` or `{ ok: false, errors, message }`, with `errors` taken from the API's problem details. |
| `src/components/SurveyForm.tsx` | Renders the visible questions and passes each one its value and error. |
| `src/components/QuestionRenderer.tsx` | Renders one question with the right control. |
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

Groups of radio buttons and checkboxes are wrapped in `<fieldset>` with a `<legend>`, labels are tied to inputs with ids from `useId`, and errors are linked with `aria-describedby` and `aria-invalid`, so the form works with screen readers.

## `SurveyForm`

| Prop | Type | Purpose |
|---|---|---|
| `questions` | `QuestionDefinition[]` | The survey's questions in order |
| `answers` | `Answers` | Answers keyed by question key |
| `errors` | `AnswerErrors` | Errors keyed by question key |
| `onChange` | `(answers: Answers) => void` | Called with the new answers after any change |

Only questions in `visibleKeys(questions, answers)` are rendered. Answers to hidden questions stay in state, and the API drops them on submit.

## Tests

Tests live in the separate `FormFlow.React.Tests` project. See [testing.md](testing.md#how-the-react-tests-work).
