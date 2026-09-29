# Backend

`FormFlow.Backend` is the ASP.NET Core API. For the endpoints themselves see [api.md](api.md). For how it fits with the other projects see [architecture.md](architecture.md).

## Configuration

`appsettings.json`, with development values in `appsettings.Development.json`:

| Setting | Default | Purpose |
|---|---|---|
| `ConnectionStrings:LiteDb` | `Filename=formflow.db;Connection=shared` | Where LiteDB stores data |
| `SeedData:DemoSurvey` | `true` | Create the demo survey on first start |
| `SeedData:SampleResponses` | `0` | Give the demo survey this many made-up responses from the last three weeks, when it has none. The public demo uses 80. |
| `SeedData:Templates` | `true` | Add the survey templates from `SeedData/templates.json` that aren't stored yet |
| `Accounts` | `Rogers` (admin) and `professor`, both with `password`, in Development; otherwise empty | A list of `{ "Username", "Password", "Role", "Email" }` accounts (`Email` is optional and lets the account reset a forgotten password). Role is `admin` or `professor`, and defaults to `professor`; an account with any other role (students don't have accounts) is skipped with a warning. Missing accounts are created at startup, and each listed account's role is updated to match. Changing a password here later has no effect on an existing account. As environment variables: `Accounts__0__Username`, `Accounts__0__Password`, `Accounts__0__Role`, and so on. |
| `SignUp:RequireApproval` | `true` | Whether professor/scientist sign-ups wait for an administrator's approval before they can sign in |
| `SignUp:RequireEmailVerification` | `true` | Whether sign-ups must open an emailed link before they can sign in |
| `Email:LinkBaseUrl` | `http://localhost:5224/` | The Blazor app's address, used for the links in emails |
| `Email:From` | `FormFlow <no-reply@formflow.local>` | The sender address |
| `Email:Smtp:Host`, `Port`, `Username`, `Password`, `EnableSsl` | Not set, `587`, not set, not set, `true` | The SMTP server that sends email. Any provider works (SendGrid, Mailgun, Amazon SES, Gmail with an app password). Without a host, emails stay in an in-memory outbox that administrators read on the Blazor **Emails** page. |
| `Jwt:Key` | A development key in Development, otherwise not set | Secret for signing tokens, at least 32 characters. When it is missing the API makes a random key at startup and logs a warning, so tokens stop working when the API restarts. |
| `Jwt:Issuer`, `Jwt:Audience` | `FormFlow` | Written into and checked on every token |
| `Jwt:LifetimeMinutes` | `480` | How long a sign-in lasts |
| `RateLimits:LoginPerMinute` | `20` | Sign-in attempts allowed per IP address per minute |
| `RateLimits:AccountPerMinute` | `20` | Sign-up, email link and password requests allowed per IP address per minute |
| `RateLimits:SubmissionsPerMinute` | `60` | Survey submissions allowed per IP address per minute |
| `Cors:AllowedOrigins` | Not set | The browser origins allowed to call the API, such as `https://gameyplum-formflow-react.onrender.com` for the React app (no trailing slash). Development allows any origin and ignores this list; elsewhere, an origin that isn't listed is refused. The Blazor app calls the API from its server, so it needs no entry. As environment variables: `Cors__AllowedOrigins__0`, `Cors__AllowedOrigins__1`, and so on. |
| `DisableHttpsRedirection` | `true` in Development, otherwise not set | Serve plain HTTP without redirecting to HTTPS. On in Development so the React app can call `http://localhost:5164`; also useful behind a proxy that terminates TLS |

Any setting can be overridden on the command line (`dotnet run --SeedData:DemoSurvey=false`) or with environment variables (`SeedData__DemoSurvey=false`).

## Database seeding

`DatabaseSeeder.Seed()` runs once at startup:

1. If the `questions` collection is empty, it loads the 13 sample questions from `SeedData/questions.json`. They cover every question type and include one conditional question (`campus_preference`, shown when `is_student` is yes).
2. If `SeedData:DemoSurvey` is on and the `surveys` collection is empty, it creates the "Student Experience Survey" with every sample question, in the order they appear in the seed file.
3. If `SeedData:SampleResponses` is above zero and the demo survey has no responses, `SampleResponseGenerator` makes that many. Each one runs through `ResponseValidator` like a real submission, so the campus question is only answered by students, and the answers lean the way a real survey might (students are younger and rate their experience higher).
4. If `SeedData:Templates` is on, it adds each template in `SeedData/templates.json` whose id isn't stored yet, as a survey with `isTemplate: true`. A template's questions are created unless a question with the same key exists, in which case that one is used. Template question keys carry a prefix per template (`course_eval_`, `event_feedback_`, `research_intake_`, `customer_sat_`) so they don't clash with anyone's own questions. Templates don't count as surveys for step 2.

To reset, stop the API and delete `formflow.db`. It is recreated and reseeded on the next start.

## Authentication

`Auth/` holds the sign-in pieces. `AdminAccountSeeder` creates the configured accounts, `TokenService` issues JWTs with the account's id (`sub`), username and `role` claim (`admin`, `professor` or `student`, listed in `FormFlow.Data/Models/Roles.cs`), and `JwtSettings` reads the `Jwt` section. `Program.cs` registers JWT bearer authentication, an `Admin` policy for reviewing sign-ups, a `Builder` policy for building surveys and reading their results (admin or professor), a `SignedIn` policy (any role), and three fixed-window rate limiters partitioned by client IP. Endpoints opt in with `.RequireAuthorization(JwtSettings.BuilderPolicy)`. After a token's signature checks out, `SessionCheck` rejects it if its account no longer exists or its password changed after the token was issued.

Emailed links use `AccountToken`s from `AccountTokenRepository` (the `account_tokens` collection): only a SHA-256 hash of each token is stored, each has a purpose and expiry, and redeeming one deletes it. `Email/` holds `AccountEmails`, which writes the verification and reset emails, and two `IEmailSender`s: `SmtpEmailSender` when `Email:Smtp:Host` is set, otherwise `OutboxEmailSender`. A failed send is logged rather than failing the request, so the person can ask for another link.

The policy only checks the role. Ownership is checked inside each endpoint: questions and surveys implement `IOwned` (`OwnerId`, `OwnerName`), creating one records the caller as its owner, and `CurrentUser.CanManage` lets an administrator manage anything and a professor only what they own, returning `403` otherwise. Items with no owner, like the seeded demo data, belong to administrators. `CurrentUser.ShowOwnerToBuilders` clears the owner fields for callers who aren't administrators or professors, since a professor's username is their email address. A submitted response stays anonymous unless the request carries a token, in which case `SubmittedBy` records the username.

`OpenApiSecurity` marks the endpoints that need a token in the OpenAPI document so Swagger UI shows the lock and the **Authorize** button.

## Services

- **`SurveyResultsBuilder`** turns a survey's responses into `SurveyResults`: per-option counts for choice, yes/no and single checkbox questions, min/max/average for number questions, and the five most recent answers for text questions. Answers to questions that were hidden count as unanswered. Given a `ResultsQuery` (from `FormFlow.Data`, read from query parameters by `ResultsQueryParser`), it first keeps only the responses with the filtered answers in the date range, then adds a timeline of them per local day, week or month, and a summary per answer group for `compareBy`. `FixedAnswers` lists the answers of the questions that can filter or split results.
- **`CsvExporter`** writes one row per response (the export endpoint passes it only the filtered responses) with a column per question key. Values with commas, quotes or line breaks are quoted, and values that a spreadsheet would treat as a formula are prefixed with `'`.

## Error responses

- Question and survey validation errors return `400` with `{ "errors": [...] }` or `{ "error": "..." }`.
- Answer validation errors return `400` as RFC 7807 problem details (`Results.ValidationProblem`) with errors keyed by question key.
- Conflicts (duplicate keys, deleting something that is in use) return `409` with a message saying what depends on the item.
- Admin endpoints return `401` without a valid token, and rate-limited endpoints return `429`.
- Unexpected errors are logged and return `500` as problem details. In Development the developer exception page adds the exception and stack trace.

## JSON schemas

`Schemas/question-definition.schema.json` and `Schemas/survey-definition.schema.json` describe the question and survey documents for anyone producing them outside the app, such as a bulk import. The survey schema references the question schema with `$ref`, so both files must stay in the same folder. The API itself validates with `QuestionValidator` and the endpoint checks described in [api.md](api.md).
