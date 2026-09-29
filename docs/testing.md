# Testing

FormFlow has four unit and integration test projects and one browser test project. Each one references only what it needs, so a Blazor test change can't pull backend dependencies into the data layer.

| Project | Framework | What it tests |
|---|---|---|
| `FormFlow.Data.Tests` | xUnit, FluentAssertions | `QuestionValidator` rules, `QuestionValidationEngine` (min/max length and value, range), `ResponseValidator`, and `VisibilityEvaluator` including chained and circular rules |
| `FormFlow.Backend.Tests` | xUnit, FluentAssertions, Moq, `WebApplicationFactory` | Every endpoint over real HTTP against an in-memory LiteDB, sign-in and which endpoints need it, email verification, password reset and change (`AccountSecurityTests`, with a clock the tests move forward to expire links), drafts, share links, close dates and one answer per browser (`SharingTests`), results filters, dates and comparisons (`ResultsAnalyticsTests`, plus `SurveyResultsBuilderTests` for timelines in local days, weeks and months), rate limits, the repositories, database seeding, and CSV escaping |
| `FormFlow.Blazor.Tests` | xUnit, bUnit, MudBlazor, RichardSzalay.MockHttp | Each question component, two-way binding through `QuestionRenderer`, the admin pages including the Share page, sign-in and the admin guard, the password and email pages, taking a survey by id or share link, and the results page with its filters, dates, timeline and comparisons |
| `FormFlow.React.Tests` | Jest, ts-jest, React Testing Library | The visibility logic, the `SurveyForm` component, and the whole app against a mocked `fetch` |
| `FormFlow.E2E` | Playwright | The running apps in a real browser: signing in, verifying an email, resetting and changing a password, the whole admin flow, taking surveys in Blazor and React, CSV download, and API security |

## Running the tests

```bash
# All .NET tests
dotnet test FormFlow.slnx

# One project
dotnet test FormFlow.Backend.Tests

# One test class
dotnet test FormFlow.Backend.Tests --filter "FullyQualifiedName~ResponseEndpointTests"

# React
cd FormFlow.React.Tests
npm install
npm test
```

## Browser tests

`FormFlow.E2E` drives the real apps with Playwright. By default it expects the docker compose stack:

```bash
docker compose up --build --detach --wait
cd FormFlow.E2E
npm ci
npx playwright install chromium
npx playwright test
```

Point it at apps running elsewhere with `API_URL`, `BLAZOR_URL` and `REACT_URL`, and at other accounts with `ADMIN_USERNAME` and `ADMIN_PASSWORD`, and `PROFESSOR_USERNAME` and `PROFESSOR_PASSWORD`. The sign-up tests create a new professor with an email unique to the run, and follow the emailed links by reading the administrator's outbox (`emailedLink` in `helpers.ts`), so the stack must run without an SMTP server. The tests share one database, so they run one at a time, and names include a run id so they can run again without a reset. The admin flow is one ordered series (create a question, edit it, build a survey, preview, publish and copy its share link, answer it as a student in a separate browser, close it, delete) in a single signed-in tab. New surveys are drafts, so API tests publish theirs with the `publish` helper before answering them anonymously.

The Blazor layout sets `data-interactive="true"` once its circuit is connected, and the tests wait for it before clicking, because clicks on a prerendered page are ignored until then. If a page never gets there, the failure message lists the browser's console errors and failed requests.

## Coverage

CI collects coverage for the .NET tests with coverlet and turns it into a report with ReportGenerator. The summary appears on each CI run's page, the HTML report is uploaded as the `coverage-report` artifact, and pushes to `main` update the badge in the README. To see it locally:

```bash
dotnet test FormFlow.slnx --collect:"XPlat Code Coverage" --results-directory coverage-raw
dotnet tool install --global dotnet-reportgenerator-globaltool
reportgenerator -reports:"coverage-raw/**/coverage.cobertura.xml" -targetdir:coverage -reporttypes:Html
```

## Checks CI runs

Every push and pull request runs two workflows in `.github/workflows/`:

- `ci.yml`: build and test the .NET solution in Release with coverage; install, test and build the React app; and build the three Docker images, start them with docker compose and run the Playwright tests against them.
- `lint.yml`: ESLint on the React app and `dotnet format FormFlow.slnx --verify-no-changes`.

Run the same checks locally before pushing:

```bash
dotnet build FormFlow.slnx
dotnet test FormFlow.slnx
dotnet format FormFlow.slnx --verify-no-changes
(cd FormFlow.React && npm run lint && npm run build)
(cd FormFlow.React.Tests && npm test)
```

## How the API tests work

`FormFlow.Backend.Tests/Endpoints/InMemoryApiFactory.cs` starts the real API with `WebApplicationFactory<Program>` and swaps the LiteDB registration for one backed by a `MemoryStream`. Each test gets a fresh database (xUnit creates a new factory per test), the 16 sample questions and the demo survey are seeded exactly as in production, and tests call the API with a normal `HttpClient`. Helpers such as `GetDemoSurveyAsync` and `GetQuestionAsync` look up seeded data by key.

Tests that should start without the demo survey set `SeedData:DemoSurvey` to `false` with `UseSetting`.

The API runs in the Development environment, so the admin account from `appsettings.Development.json` is created too. `AdminClient.AsAdmin()` signs a client in with it, and `AuthEndpointTests` checks every admin endpoint answers `401` without a token.

## How the Blazor tests work

Component tests render with bUnit. Tests that use MudBlazor create a context per test with `await using var ctx = new BunitContext();`, because MudBlazor registers services that can only be disposed asynchronously. Pages that call the API get a fake `IQuestionService` or `ISurveyService` (see `Respond/FakeSurveyService.cs`), so page tests don't need a running backend. `Auth/FakeSessionStorage.cs` stands in for the browser's session storage, so the sign-in tests cover the real encryption and restore logic.

## How the React tests work

`FormFlow.React.Tests` imports the app's source from `../FormFlow.React/src`. `jest.config.cjs` maps `react` and `react-dom` to the test project's own `node_modules`, so only one copy of React is loaded, and maps CSS imports to a stub. `App.test.tsx` replaces `global.fetch` with canned API responses to test the survey list, conditional questions, server errors and the thank-you screen.

## Manual checks

- Swagger UI at `http://localhost:5164/swagger` can send every request.
- `FormFlow.Backend/backend.http` has example requests, including invalid submissions, for the VS Code REST Client or Rider.
