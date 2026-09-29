# Backend

`FormFlow.Backend` is the ASP.NET Core API. For the endpoints themselves see [api.md](api.md). For how it fits with the other projects see [architecture.md](architecture.md).

## Configuration

`appsettings.json`, with development values in `appsettings.Development.json`:

| Setting | Default | Purpose |
|---|---|---|
| `ConnectionStrings:LiteDb` | `Filename=formflow.db;Connection=shared` | Where LiteDB stores data |
| `SeedData:DemoSurvey` | `true` | Create the demo survey on first start |
| `Admin:Username`, `Admin:Password` | `admin` / `formflow-admin` in Development, otherwise not set | The admin account created on first start when there are no accounts yet. Changing them later has no effect on an existing database. |
| `Jwt:Key` | A development key in Development, otherwise not set | Secret for signing tokens, at least 32 characters. When it is missing the API makes a random key at startup and logs a warning, so tokens stop working when the API restarts. |
| `Jwt:Issuer`, `Jwt:Audience` | `FormFlow` | Written into and checked on every token |
| `Jwt:LifetimeMinutes` | `480` | How long a sign-in lasts |
| `RateLimits:LoginPerMinute` | `20` | Sign-in attempts allowed per IP address per minute |
| `RateLimits:SubmissionsPerMinute` | `60` | Survey submissions allowed per IP address per minute |
| `DisableHttpsRedirection` | `true` in Development, otherwise not set | Serve plain HTTP without redirecting to HTTPS. On in Development so the React app can call `http://localhost:5164`; also useful behind a proxy that terminates TLS |

Any setting can be overridden on the command line (`dotnet run --SeedData:DemoSurvey=false`) or with environment variables (`SeedData__DemoSurvey=false`).

## Database seeding

`DatabaseSeeder.Seed()` runs once at startup:

1. If the `questions` collection is empty, it loads the 10 sample questions from `SeedData/questions.json`. They cover every question type and include one conditional question (`campus_preference`, shown when `is_student` is yes).
2. If `SeedData:DemoSurvey` is on and the `surveys` collection is empty, it creates the "Student Experience Survey" with every sample question, in the order they appear in the seed file.

To reset, stop the API and delete `formflow.db`. It is recreated and reseeded on the next start.

## Authentication

`Auth/` holds the sign-in pieces. `AdminAccountSeeder` creates the first admin, `TokenService` issues JWTs with a `role: admin` claim, and `JwtSettings` reads the `Jwt` section. `Program.cs` registers JWT bearer authentication, an `Admin` authorization policy that requires that role, and two fixed-window rate limiters partitioned by client IP. Endpoints opt in with `.RequireAuthorization(JwtSettings.AdminPolicy)`, and `OpenApiSecurity` marks those endpoints in the OpenAPI document so Swagger UI shows the lock and the **Authorize** button.

## Services

- **`SurveyResultsBuilder`** turns a survey's responses into `SurveyResults`: per-option counts for choice, yes/no and single checkbox questions, min/max/average for number questions, and the five most recent answers for text questions. Answers to questions that were hidden count as unanswered.
- **`CsvExporter`** writes one row per response with a column per question key. Values with commas, quotes or line breaks are quoted, and values that a spreadsheet would treat as a formula are prefixed with `'`.

## Error responses

- Question and survey validation errors return `400` with `{ "errors": [...] }` or `{ "error": "..." }`.
- Answer validation errors return `400` as RFC 7807 problem details (`Results.ValidationProblem`) with errors keyed by question key.
- Conflicts (duplicate keys, deleting something that is in use) return `409` with a message saying what depends on the item.
- Admin endpoints return `401` without a valid token, and rate-limited endpoints return `429`.

## JSON schemas

`Schemas/question-definition.schema.json` and `Schemas/survey-definition.schema.json` describe the question and survey documents for anyone producing them outside the app, such as a bulk import. The survey schema references the question schema with `$ref`, so both files must stay in the same folder. The API itself validates with `QuestionValidator` and the endpoint checks described in [api.md](api.md).
