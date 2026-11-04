# Blazor Components

The Blazor app (`FormFlow.Blazor`) renders questions it gets from the API. Each question type has its own component, a renderer picks the right one at runtime, and `SurveyForm` puts them together into a working form.

## Question components

All fourteen live in `Components/QuestionTypes/` and derive from `QuestionComponentBase`.

| Type | Component | Renders |
|---|---|---|
| `text` | `TextQuestion` | `MudTextField` with placeholder and help text |
| `number` | `NumberQuestion` | Numeric input |
| `yes_no` | `YesNoQuestion` | Yes and No radio buttons |
| `dropdown` | `DropdownQuestion` | `MudSelect` over the options |
| `radio` | `RadioQuestion` | `MudRadioGroup` over the options |
| `checkbox` | `CheckboxQuestion` | One tick box, or one per option when options are set |
| `multiselect` | `MultiselectQuestion` | A tick box per option |
| `long_text` | `LongTextQuestion` | Multi-line `MudTextField` |
| `email` | `EmailQuestion` | `MudTextField` with an email input |
| `date` | `DateQuestion` | Native date input, which reports ISO dates |
| `rating` | `RatingQuestion` | `MudRating` with the question's number of stars |
| `likert` | `LikertQuestion` | A grid with a row per statement and a column per point of the scale. Each row is a `MudRadioGroup` labelled by its statement, and each radio button carries its column's label. It scrolls sideways inside the card on narrow screens. |
| `nps` | `NpsQuestion` | Eleven buttons from 0 to 10, with "Not likely" and "Extremely likely" under the ends. The chosen one is pressed (`aria-pressed`); pressing it again clears the answer. |
| `slider` | `SliderQuestion` | `MudSlider` from the question's minimum to maximum value (0 to 100 by default), with the value beside it. Unanswered until it is moved. |

### `QuestionComponentBase` parameters

| Parameter | Type | Purpose |
|---|---|---|
| `Question` | `QuestionDefinition` | Required. What to render. |
| `Value` | `IReadOnlyList<string>?` | The current answer. Every type uses a list of strings, matching how the API stores answers. |
| `ValueChanged` | `EventCallback<IReadOnlyList<string>>` | Raised when the respondent changes the answer. |
| `Error` | `string?` | A message shown under the question, such as a server validation error. |

Components call `ReportAsync(...)` from the base class to raise `ValueChanged`, and re-sync their internal state from `Value` in `OnParametersSet`, so a parent can reset or pre-fill answers. Without a `ValueChanged` handler a component keeps its own state, which is how standalone previews work.

## `QuestionRenderer`

`QuestionRenderer` takes a `QuestionDefinition`, asks `QuestionComponentMapper.Resolve(type)` for the component type, and renders it with `DynamicComponent`. It passes `Value`, `ValueChanged` and `Error` through only when its own `ValueChanged` is bound. An unknown type renders "Unsupported question type: …" instead of throwing.

To add a question type:

1. Add the type name to `QuestionTypes` in `FormFlow.Data`, and to the validators if it needs new rules.
2. Create a component in `Components/QuestionTypes/` that inherits `QuestionComponentBase`.
3. Add it to the `switch` in `QuestionComponentMapper`.
4. Add a bUnit test next to the others in `FormFlow.Blazor.Tests/Components/`.

## `SurveyForm`

`Components/SurveyForm.razor` renders a list of questions as one form.

| Parameter | Type | Purpose |
|---|---|---|
| `Questions` | `IReadOnlyList<QuestionDefinition>` | The survey's questions in order |
| `Answers` | `Dictionary<string, List<string>>` | Answers keyed by question key; the form updates it in place |
| `Errors` | `IReadOnlyDictionary<string, string[]>?` | Errors keyed by question key, usually from the API's 400 response |
| `AnswersChanged` | `EventCallback` | Raised after any answer changes |
| `Page` | `IReadOnlyCollection<QuestionDefinition>?` | When set, only these questions are drawn (one page); visibility still uses every answer |

After every change it runs `VisibilityEvaluator.VisibleKeys` from `FormFlow.Data`, so conditional questions appear or disappear straight away.

## `PagedSurveyForm`

`Components/PagedSurveyForm.razor` wraps `SurveyForm` for the take-survey page and the admin preview. It splits the questions at the survey's `PageBreaks` with `SurveyPaging` (in `FormFlow.Data`) and shows one page at a time with "Page 2 of 3", a progress bar, **Back**, **Next** and, on the last page, **Submit**. Pages whose questions are all hidden are skipped, and the progress only shows while more than one page is shown. **Next** runs the page's answers through the same `ResponseValidator` the API uses, so it shows the server's messages without a round trip. When a new set of server errors arrives, it opens the first page with one. Without page breaks it draws `SurveyForm` and a Submit button exactly as before. Its parameters are `Questions`, `PageBreaks`, `Answers`, `Errors`, `AnswersChanged`, `OnSubmit` (no Submit button without it, as in the preview) and `Submitting`.

## Quiz components

- `Components/Shared/QuizScoreCard.razor` shows a respondent's grade on the take-survey thank-you screen. Its one parameter, `Result`, is the `QuizResult` from the submit response; it draws nothing when that is null. It shows "Your score" with the points, the percentage (when anything was graded) and a progress bar, then, when the result lists questions, "Review your answers" with a card per question: Correct, Partly correct or Incorrect, the points earned out of the question's points, the respondent's answer, the correct answer when they missed it, and the explanation.
- `Components/Shared/QuizResultsPanel.razor` is the **Quiz scores** card on the results page. Its `Summary` parameter is the results' `QuizSummary`; it draws nothing for a survey that isn't a quiz. It shows the average score and percentage, the score distribution as a bar per band, and a table of each graded question's points, correct answers and average points.
- `Services/QuizText.cs` formats points without trailing zeros ("7.5") and scores as "8 of 10 points" or "1 of 1 point", for both components.

## Pages

- `Pages/Home.razor`: landing page with links to the surveys and to joining with a code.
- `Pages/Respond/JoinSurvey.razor` (`/join`): a field for the code a professor shows, set up for phones (no autocorrect or spellcheck, capitals on, a **Go** key). It checks the code with `ShareCodes.LooksValid` from `FormFlow.Data`, then looks it up and opens `/s/{code}`.
- `Pages/Respond/SurveyList.razor` (`/surveys`): the published surveys.
- `Pages/Respond/TakeSurvey.razor` (`/surveys/{id}` and share links, `/s/{code}`): loads the survey, renders `PagedSurveyForm`, submits to the API with the browser's respondent id, shows errors per question, and shows a thank-you screen on success, with `QuizScoreCard` for a quiz (and a notice above the form that it's graded). When the survey shares its results, the thank-you screen (and the closed and already-answered messages) adds `RespondentResultsSummary`. After the first render it restores answers saved in the browser by `SurveyDrafts` (encrypted local storage, one entry per survey), shows "We saved your answers on this device." with **Start over**, saves answers as they change, and clears them once they are sent. A closed survey, or one this browser already answered, shows a message instead of the form; a draft shows a notice to the people who can open it.
- `Pages/Login.razor`, `SignUp.razor`, `ForgotPassword.razor`, `ResetPassword.razor`, `VerifyEmail.razor` and `AccountSettings.razor`: signing in and managing an account, see [admin.md](admin.md#signing-in). The pages that open an emailed link redeem its token after the first interactive render, not during prerendering, so the one-time link isn't used up by a page load the visitor never sees.
- `Pages/Admin/*`: see [admin.md](admin.md). `AdminSurveyPresent.razor` (`/admin/surveys/{id}/present`) uses `Layout/PresenterLayout.razor`, which has the providers and the admin guard but no menu or app bar, so it fills a projector.

## Shared pieces

- `Shared/ShareQrCode.razor`: the QR code of a share link, drawn as an inline SVG and cached per link. The Share page and the presenter view use it.
- `Shared/OptionBars.razor`: a bar per answer with its count and percentage, from a list of `OptionBar`s (`OptionBar.Of` makes them from results or a respondent summary). `Large` stacks each label over its bar in bigger type for the presenter view. The results page, the presenter view and the respondent summary use it.
- `RespondentResultsSummary.razor`: "Results so far" for a survey id, loaded from the public summary endpoint, with "Based on N responses", the bars of each question, and **Show the latest**. It draws nothing when the survey doesn't share its results.

## Services

The pages talk to the API through typed `HttpClient` services registered in `Program.cs`. The base address comes from `BackendApi:BaseUrl` in `appsettings.json`.

| Service | Methods |
|---|---|
| `IQuestionService` | `GetAllQuestionsAsync`, `GetQuestionAsync`, `CreateQuestionAsync`, `UpdateQuestionAsync`, `DeleteQuestionAsync` |
| `ISurveyService` | `GetSurveysAsync`, `GetSurveyAsync`, `GetSurveyByShareCodeAsync`, `GetManagedSurveysAsync`, `GetSurveyQuestionsAsync`, `CreateSurveyAsync`, `UpdateSurveyAsync`, `DeleteSurveyAsync`, `DuplicateSurveyAsync`, `GetTemplatesAsync`, `UseTemplateAsync`, `UpdateSharingAsync`, `SubmitResponseAsync`, `HasAnsweredAsync`, `GetRespondentResultsAsync` (the public summary, or null), `GetResultsAsync` and `ExportResponsesAsync` (both take an optional `ResultsQuery` of filters, dates and comparison) |
| `IAuthService` | `LoginAsync`, `SignUpAsync`, `RequestPasswordResetAsync`, `ResendVerificationAsync`, `VerifyEmailAsync`, `ResetPasswordAsync`, `ChangePasswordAsync` |
| `IAccountService` | `GetPendingAsync`, `ApproveAsync`, `DeclineAsync`, `GetOutboxAsync` (administrators) |
| `ILiveResultsClient` | `WatchAsync(surveyId, cancellationToken)`: an `IAsyncEnumerable<SurveyResults>` read from the live results stream with the signed-in builder's token. It throws `LiveResultsRefusedException` for 401, 403 and 404, and `TimeoutException` when the stream is silent for `IdleTimeout` (45 seconds). Registered with no `HttpClient` timeout, since the stream stays open. |
| `LiveResultsConnection` | Not a service: the presenter view makes one per survey. `RunAsync` watches the stream until cancelled, raising `ResultsReceived` and `StateChanged` (`Connecting`, `Live`, `Reconnecting`, `Stopped`), and reconnects after 1, 2, 4… seconds, up to 15. A refusal stops it with a `StopReason`. |
| `IRespondentIdentity` | `GetIdAsync`: the browser's random respondent id, kept in encrypted local storage |
| `ISurveyDrafts` | `LoadAsync`, `SaveAsync`, `ClearAsync`: a survey's unsent answers, kept in encrypted local storage |

Create, update and delete return `(bool Success, string? Error)`, with the API's error message turned into readable text. The account methods return a `FormResult` with errors per field, so each page shows them under the matching input. `SubmitResponseAsync` returns a `SubmitResult` with the per-question errors from a 400 response, `CanRetry: false` when the survey closed, is gone, or was already answered, and `Quiz`, the grade from the 201 body, for a quiz. Tests replace these interfaces with fakes, so page tests don't need a running API.
