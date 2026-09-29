# Blazor Components

The Blazor app (`FormFlow.Blazor`) renders questions it gets from the API. Each question type has its own component, a renderer picks the right one at runtime, and `SurveyForm` puts them together into a working form.

## Question components

All eleven live in `Components/QuestionTypes/` and derive from `QuestionComponentBase`.

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

## Pages

- `Pages/Home.razor`: landing page with a link to the surveys.
- `Pages/Respond/SurveyList.razor` (`/surveys`): the published surveys.
- `Pages/Respond/TakeSurvey.razor` (`/surveys/{id}` and share links, `/s/{code}`): loads the survey, renders `PagedSurveyForm`, submits to the API with the browser's respondent id, shows errors per question, and shows a thank-you screen on success. After the first render it restores answers saved in the browser by `SurveyDrafts` (encrypted local storage, one entry per survey), shows "We saved your answers on this device." with **Start over**, saves answers as they change, and clears them once they are sent. A closed survey, or one this browser already answered, shows a message instead of the form; a draft shows a notice to the people who can open it.
- `Pages/Login.razor`, `SignUp.razor`, `ForgotPassword.razor`, `ResetPassword.razor`, `VerifyEmail.razor` and `AccountSettings.razor`: signing in and managing an account, see [admin.md](admin.md#signing-in). The pages that open an emailed link redeem its token after the first interactive render, not during prerendering, so the one-time link isn't used up by a page load the visitor never sees.
- `Pages/Admin/*`: see [admin.md](admin.md).

## Services

The pages talk to the API through typed `HttpClient` services registered in `Program.cs`. The base address comes from `BackendApi:BaseUrl` in `appsettings.json`.

| Service | Methods |
|---|---|
| `IQuestionService` | `GetAllQuestionsAsync`, `GetQuestionAsync`, `CreateQuestionAsync`, `UpdateQuestionAsync`, `DeleteQuestionAsync` |
| `ISurveyService` | `GetSurveysAsync`, `GetSurveyAsync`, `GetSurveyByShareCodeAsync`, `GetManagedSurveysAsync`, `GetSurveyQuestionsAsync`, `CreateSurveyAsync`, `UpdateSurveyAsync`, `DeleteSurveyAsync`, `DuplicateSurveyAsync`, `GetTemplatesAsync`, `UseTemplateAsync`, `UpdateSharingAsync`, `SubmitResponseAsync`, `HasAnsweredAsync`, `GetResultsAsync` and `ExportResponsesAsync` (both take an optional `ResultsQuery` of filters, dates and comparison) |
| `IAuthService` | `LoginAsync`, `SignUpAsync`, `RequestPasswordResetAsync`, `ResendVerificationAsync`, `VerifyEmailAsync`, `ResetPasswordAsync`, `ChangePasswordAsync` |
| `IAccountService` | `GetPendingAsync`, `ApproveAsync`, `DeclineAsync`, `GetOutboxAsync` (administrators) |
| `IRespondentIdentity` | `GetIdAsync`: the browser's random respondent id, kept in encrypted local storage |
| `ISurveyDrafts` | `LoadAsync`, `SaveAsync`, `ClearAsync`: a survey's unsent answers, kept in encrypted local storage |

Create, update and delete return `(bool Success, string? Error)`, with the API's error message turned into readable text. The account methods return a `FormResult` with errors per field, so each page shows them under the matching input. `SubmitResponseAsync` returns a `SubmitResult` with the per-question errors from a 400 response, and `CanRetry: false` when the survey closed, is gone, or was already answered. Tests replace these interfaces with fakes, so page tests don't need a running API.
