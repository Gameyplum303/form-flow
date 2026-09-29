# Testing

FormFlow has four test projects. Each one references only what it needs, so a Blazor test change can't pull backend dependencies into the data layer.

| Project | Framework | What it tests |
|---|---|---|
| `FormFlow.Data.Tests` | xUnit, FluentAssertions | `QuestionValidator` rules, `QuestionValidationEngine` (min/max length and value, range), `ResponseValidator`, and `VisibilityEvaluator` including chained and circular rules |
| `FormFlow.Backend.Tests` | xUnit, FluentAssertions, Moq, `WebApplicationFactory` | Every endpoint over real HTTP against an in-memory LiteDB, the repositories, database seeding, and CSV escaping |
| `FormFlow.Blazor.Tests` | xUnit, bUnit, MudBlazor, RichardSzalay.MockHttp | Each question component, two-way binding through `QuestionRenderer`, the admin pages, taking a survey, and the results page |
| `FormFlow.React.Tests` | Jest, ts-jest, React Testing Library | The visibility logic, the `SurveyForm` component, and the whole app against a mocked `fetch` |

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

## Checks CI runs

Every push and pull request runs two workflows in `.github/workflows/`:

- `ci.yml`: restore, build and test the .NET solution in Release, and install, test and build the React app.
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

`FormFlow.Backend.Tests/Endpoints/InMemoryApiFactory.cs` starts the real API with `WebApplicationFactory<Program>` and swaps the LiteDB registration for one backed by a `MemoryStream`. Each test gets a fresh database (xUnit creates a new factory per test), the 10 sample questions and the demo survey are seeded exactly as in production, and tests call the API with a normal `HttpClient`. Helpers such as `GetDemoSurveyAsync` and `GetQuestionAsync` look up seeded data by key.

Tests that should start without the demo survey set `SeedData:DemoSurvey` to `false` with `UseSetting`.

## How the Blazor tests work

Component tests render with bUnit. Tests that use MudBlazor create a context per test with `await using var ctx = new BunitContext();`, because MudBlazor registers services that can only be disposed asynchronously. Pages that call the API get a fake `IQuestionService` or `ISurveyService` (see `Respond/FakeSurveyService.cs`), so page tests don't need a running backend.

## How the React tests work

`FormFlow.React.Tests` imports the app's source from `../FormFlow.React/src`. `jest.config.cjs` maps `react` and `react-dom` to the test project's own `node_modules`, so only one copy of React is loaded, and maps CSS imports to a stub. `App.test.tsx` replaces `global.fetch` with canned API responses to test the survey list, conditional questions, server errors and the thank-you screen.

## Manual checks

- Swagger UI at `http://localhost:5164/swagger` can send every request.
- `FormFlow.Backend/backend.http` has example requests, including invalid submissions, for the VS Code REST Client or Rider.
