# API Reference

The API is an ASP.NET Core minimal API in `FormFlow.Backend`. Endpoints are grouped by resource in `FormFlow.Backend/Endpoints/`.

| Environment | Base URL |
|---|---|
| `dotnet run` (HTTP) | `http://localhost:5164` |
| `dotnet run` (HTTPS) | `https://localhost:7209` |
| `docker compose up` | `http://localhost:5164` |

- OpenAPI document: `/openapi/v1.json`
- Swagger UI: `/swagger` (the root URL `/` redirects here)
- Ready-to-send requests: [`FormFlow.Backend/backend.http`](../FormFlow.Backend/backend.http)

All bodies are JSON with camelCase property names. Ids are GUIDs.

---

## Authentication

Reading questions and surveys and submitting answers are public. Everything else needs a token from signing in, and every account has a role:

| Role | Shown as | Can |
|---|---|---|
| `admin` | Administrator | Everything: create, edit and delete any question or survey, and read every survey's responses and results |
| `professor` | Professor/Scientist | Create questions and surveys, and edit, delete and read the responses and results of the ones they created |
| `student` | Student | Take surveys. Students don't have accounts; the role exists so administrators can preview the site as a student. |

Questions and surveys record who created them in `ownerId` and `ownerName`. The seeded demo data has no owner and belongs to administrators. Every professor can put any question in their surveys, but only change their own.

Endpoints marked **Builder** need the `admin` or `professor` role; for a professor they return `403` on a question or survey someone else created. Endpoints marked **Admin** need the `admin` role. Endpoints marked **Signed in** accept any role. Both return `401` without a valid token.

### `POST /api/auth/login`

```json
{ "username": "Rogers", "password": "password" }
```

| Status | When |
|---|---|
| 200 | `{ "token": "eyJ…", "userId": "…", "username": "Rogers", "role": "admin", "expiresAt": "2026-09-29T20:00:00Z" }` |
| 401 | Problem details titled "Invalid username or password." The same answer is given for an unknown user and a wrong password, and both take the same time. |
| 403 | The password is right but the account can't sign in yet. Problem details with a `reason`: `email_unverified` ("Please verify your email address first.") until the email link is opened, then `pending` ("Your account is waiting for an administrator's approval.") until an administrator approves it. A wrong password always gets the 401, so these never reveal an account to someone without its password. |
| 429 | More than `RateLimits:LoginPerMinute` attempts from one IP address in a minute |

Professors and scientists who signed up sign in with their email address as the username.

Send the token on admin requests:

```
Authorization: Bearer eyJ…
```

Tokens are signed JWTs carrying the account's id (`sub`), username and role and last `Jwt:LifetimeMinutes` (8 hours by default). In Swagger UI, sign in with the login endpoint, then paste the token into **Authorize**.

A token stops working before it expires when its account is deleted, or when the account's password is reset or changed after the token was issued. Every request checks this, so a password change signs the account out everywhere else.

### `GET /api/auth/me` (Signed in)

Returns `{ "id": "…", "username": "Rogers", "role": "admin" }` for the token's user, so a client can check that its token is still valid.

### `POST /api/auth/signup`

Asks for a professor/scientist account. Administrators are added through configuration, and students take surveys without an account.

```json
{
  "name": "Ada Lovelace",
  "email": "ada@lab.example",
  "password": "analytical",
  "dateOfBirth": "1990-12-10",
  "intendedUse": "Surveys for my lab's study participants.",
  "organization": "Analytical Engines Lab"
}
```

| Status | When |
|---|---|
| 201 | `{ "status": "pending", "emailVerificationRequired": true }`: the API emails a verification link, and the account waits for an administrator. With `SignUp:RequireApproval` set to `false` the status is `"active"`, and with `SignUp:RequireEmailVerification` set to `false` no email is sent and `emailVerificationRequired` is `false`. |
| 400 | Validation problem details with `errors` keyed by field: every field is required, `email` must look like an email address, `password` needs at least 8 characters, `dateOfBirth` must be an ISO date at least 18 years ago, and `name`, `organization` and `intendedUse` are limited to 100, 200 and 1000 characters |
| 409 | Validation problem details with an `email` error: an account with this email already exists |
| 429 | More than `RateLimits:AccountPerMinute` account requests from one IP address in a minute |

### Email links

Verification and password reset work through links the API emails to the account's address. A link carries a single-use token: 32 random bytes, of which the database keeps only a SHA-256 hash. A verification link lasts 2 days and a reset link 1 hour, sending a new link cancels the older ones, and trying a token uses it up whether it matched or not. Links point at the Blazor app (`Email:LinkBaseUrl`), which calls the endpoints below. Every endpoint in this section shares the `RateLimits:AccountPerMinute` limit (429), separate from sign-in.

| Endpoint | Body | Returns |
|---|---|---|
| `POST /api/auth/verify-email` | `{ "token": "…" }` | 200 `{ "status": "pending" }` (or `"active"`) once the email is verified; 400 "This link is invalid or has expired. Ask for a new one." |
| `POST /api/auth/resend-verification` | `{ "email": "…" }` | 202 `{ "message": "If an account uses that email address, we've sent it a link." }`; a new link goes only to an account that is still unverified |
| `POST /api/auth/forgot-password` | `{ "email": "…" }` | The same 202; a reset link goes to the account with that email, if there is one |
| `POST /api/auth/reset-password` | `{ "token": "…", "password": "…" }` | 204; 400 with a `password` error (the link still works) or the invalid-link problem. A reset also verifies the email, and signs the account out everywhere. |

The resend and forgot endpoints give the same answer whether or not the email has an account, so they can't be used to find out who has signed up.

### `POST /api/auth/change-password` (Signed in)

```json
{ "currentPassword": "password", "newPassword": "a-new-password" }
```

Returns 200 with a new sign-in (the same body as `/login`), because the change ends every earlier token, including the one that made this request. Returns 400 with a `currentPassword` error when it is wrong, or a `newPassword` error when the new one is too short or too long (8 to 128 characters).

### `GET /api/accounts/pending` (Admin)

Sign-ups waiting for approval, oldest first: `[{ "id": "…", "name": "…", "email": "…", "dateOfBirth": "1990-12-10", "intendedUse": "…", "organization": "…", "emailVerified": true, "createdAt": "…" }]`.

### `POST /api/accounts/{id}/approve` and `POST /api/accounts/{id}/decline` (Admin)

Approving lets the account sign in (once its email is verified). Declining deletes the sign-up and its email links, so the person can sign up again. Both return 204, or 404 when there is no sign-up waiting with that id (active accounts can't be declined).

### `GET /api/accounts/outbox` (Admin)

When no SMTP server is configured, emails aren't sent anywhere: they stay in memory (the newest 100, cleared when the API restarts) and are written to the log. This returns them newest first, `[{ "to": "…", "subject": "…", "body": "…", "sentAt": "…" }]`, so a demo or test can follow the links. It returns 404 once `Email:Smtp:Host` is set.

### Accounts

Passwords are stored as salted PBKDF2 hashes (ASP.NET Core Identity's `PasswordHasher`) in the `users` collection. At startup the API creates each account listed under `Accounts` that doesn't exist yet, and sets each listed account's role to match configuration. Usernames are matched without regard to case and shown as they were written. A configured account can have an `Email`, which lets it use forgot-password. See [backend.md](backend.md#configuration).

---

## Questions

### `GET /api/questions`

Returns every question in the question bank.

### `GET /api/questions/{id}`

| Status | When |
|---|---|
| 200 | The question |
| 400 | `id` is not a GUID: `{ "error": "Invalid question id. Provide a non-empty GUID value." }` |
| 404 | No question with that id |

### `POST /api/questions` (Builder)

```json
{
  "key": "favorite_language",
  "label": "Favorite programming language",
  "type": "radio",
  "required": true,
  "helpText": "Pick the one you use most.",
  "options": [
    { "label": "C#", "value": "csharp" },
    { "label": "TypeScript", "value": "typescript" }
  ],
  "visibleIf": { "key": "is_student", "shouldEqual": true },
  "validationConfigs": null
}
```

| Status | When |
|---|---|
| 201 | Created. Body is the stored question with its new `id`, owned by the caller. |
| 400 | `{ "errors": ["..."] }` listing every rule the question breaks (see below) |
| 409 | A question with that `key` already exists |

Rules checked on create and update:

- `key`, `label` and `type` are required, and `type` must be one of `text`, `number`, `yes_no`, `dropdown`, `radio`, `checkbox`, `multiselect`, `long_text`, `email`, `date`, `rating`.
- A `rating` can set its number of stars with a `MaxValue` rule from 2 to 10.
- `dropdown`, `radio` and `multiselect` need at least one option. A `checkbox` with no options is a single tick box.
- Option labels and values are required and must be unique within the question.
- `visibleIf.key` must name an existing `yes_no` question other than this one.
- `validationConfigs`, when present, must be a JSON array of rules with a known `validationType` (`MinLength`, `MaxLength`, `MinValue`, `MaxValue`, `Range`). See [question-definition.md](question-definition.md).

### `PUT /api/questions/{id}` (Builder)

Same body and rules as `POST`.

| Status | When |
|---|---|
| 200 | Updated question |
| 400 | `{ "errors": [...] }` |
| 403 | A professor editing a question someone else created |
| 404 | No question with that id |
| 409 | `{ "error": "..." }` when the new key belongs to another question, or when the key changes while a survey or another question's `visibleIf` still uses it. Stored answers are keyed by question key, so a key in use stays fixed. |

### `DELETE /api/questions/{id}` (Builder)

| Status | When |
|---|---|
| 204 | Deleted |
| 403 | A professor deleting a question someone else created |
| 404 | No question with that id |
| 409 | `{ "error": "..." }` naming the surveys that use it, or the questions whose `visibleIf` depends on it |

---

## Surveys

A survey is a title, a description and an ordered list of question ids. The API adds its sharing settings: `status` (`draft` or `published`), `listed`, `shareCode` and `closesAt`.

```json
{
  "title": "Customer Feedback",
  "description": "Tell us how we did.",
  "questionIds": [
    "b5d8f0e1-1c5b-4f7b-8cfa-6cab5f7fd001",
    "a12e5b95-5f5b-4d95-ae01-1ca3ea6d0005"
  ]
}
```

| Method | Path | Access | Result |
|---|---|---|---|
| `GET` | `/api/surveys` | Public | The public list: published, listed surveys that haven't closed |
| `GET` | `/api/surveys/{id}` | Public | One survey; 400 if `id` is not a GUID, 404 if missing or a draft the caller can't manage |
| `GET` | `/api/share/{code}` | Public | The survey with that share code (not case sensitive); 404 if there is none or it's a draft the caller can't manage |
| `GET` | `/api/surveys/{id}/questions` | Public | The survey's questions in survey order, so a client can render it with one call; 404 like `GET /api/surveys/{id}` |
| `GET` | `/api/surveys/managed` | Builder | The surveys the caller can manage, newest first: every survey for an administrator, their own for a professor |
| `POST` | `/api/surveys` | Builder | 201 with the stored survey, owned by the caller, or 400 `{ "error": "..." }` |
| `PUT` | `/api/surveys/{id}` | Builder | 200 with the updated survey (keeps `createdAt` and the owner), 400, 403, or 404 |
| `DELETE` | `/api/surveys/{id}` | Builder | 204, also deleting the survey's responses; 403 or 404 |
| `PUT` | `/api/surveys/{id}/sharing` | Builder | 200 with the updated survey; 400 for an unknown status, 403 or 404 |

A survey is rejected with 400 when the title or description is empty, when it has no questions, when a question appears twice, or when a question id doesn't exist.

### Sharing

A new survey is a `draft` with `listed: false` and a random 8-character `shareCode`. Only the people who manage a draft (its owner and administrators) can open or answer it. Its share link is `/s/{shareCode}` in the Blazor app and `#/s/{shareCode}` in the React app. Editing a survey keeps its sharing settings, which change only through `PUT /api/surveys/{id}/sharing`:

```json
{ "status": "published", "listed": false, "closesAt": "2026-10-15T17:00:00Z" }
```

- `published` surveys can be opened and answered by anyone with the link, without an account.
- `listed` surveys also appear in `GET /api/surveys`. A draft is never listed.
- `closesAt` (UTC, optional) is when the survey stops taking answers. It drops off the public list then, and submissions get 409.

Surveys stored before sharing existed have no status and are treated as published and listed.

---

## Responses and results

### `POST /api/surveys/{id}/responses`

Submits one set of answers, keyed by question key. An answer can be a string, number, boolean, `null`, or an array of those for checkbox and multiselect questions.

```json
{
  "answers": {
    "first_name": "Ada",
    "age": 28,
    "is_student": true,
    "campus_preference": "north",
    "skills": ["csharp", "sql"]
  },
  "respondentId": "4f9c2b7e8d1a4c3b9e6f0a2d5c8b7e1f"
}
```

`respondentId` is optional, up to 64 characters. Respondents have no account, so the Blazor and React apps each keep a random id per browser and send it; the API then refuses a second answer from the same id. Clearing the browser's storage starts a new id, so this stops accidental repeats rather than determined ones. `GET /api/surveys/{id}/answered?respondentId=...` returns `{ "answered": true }` or `false`, so a client can show a thank-you instead of the form.

The server validates the whole submission against the survey's questions:

- Answers for hidden questions (their `visibleIf` rule is not met) are dropped, and hidden questions are never required.
- Required visible questions must have an answer.
- Unknown keys are rejected.
- `number` answers must be numbers, `yes_no` answers must be `true`/`false` (or `yes`/`no`), and choice answers must be one of the question's option values with no duplicates.
- `email` answers must look like an email address, `date` answers must be ISO dates (`YYYY-MM-DD`), and `rating` answers must be a whole number of stars from 1 to the question's maximum.
- Text, long text and number answers are checked against the question's `validationConfigs` rules.

| Status | When |
|---|---|
| 201 | Stored. Body is the saved response with normalized answers. Taking a survey needs no account; when the request carries a token, `submittedBy` records the username. |
| 400 | RFC 7807 validation problem, with one entry per question key |
| 404 | No survey with that id, or a draft the caller can't manage |
| 409 | `{ "error": "..." }` when the survey has closed or this `respondentId` already answered |
| 429 | More than `RateLimits:SubmissionsPerMinute` submissions from one IP address in a minute |

Example 400 body:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "last_name": ["This question is required."],
    "age": ["Value must be ≤ 120."]
  }
}
```

### `GET /api/surveys/{id}/responses` (Builder)

All stored responses for the survey, oldest first. 404 if the survey doesn't exist.

### `GET /api/surveys/{id}/results` (Builder)

Aggregated results for the admin results page:

```json
{
  "surveyId": "…",
  "title": "Student Experience Survey",
  "totalResponses": 6,
  "lastSubmittedAt": "2026-09-29T12:58:00Z",
  "questions": [
    {
      "key": "is_student", "label": "Are you currently a student?", "type": "yes_no",
      "answeredCount": 6,
      "options": [ { "value": "true", "label": "Yes", "count": 4 }, { "value": "false", "label": "No", "count": 2 } ]
    },
    {
      "key": "age", "type": "number", "answeredCount": 6,
      "numbers": { "min": 19, "max": 45, "average": 28.17 }
    },
    {
      "key": "first_name", "type": "text", "answeredCount": 6,
      "recentAnswers": ["Finn", "Emma", "Dev", "Chloe", "Ben"]
    }
  ]
}
```

Choice and yes/no questions get a count per option, number questions get min, max and average, and text questions get the five most recent answers.

### `GET /api/surveys/{id}/responses/export` (Builder)

Downloads every response as `<survey-title>-responses.csv`. Columns are `response_id`, `submitted_at`, `submitted_by`, then one column per question key in survey order. Multiple values are joined with `; `. Cells that start with `=`, `+`, `-`, `@`, a tab or a carriage return (and aren't numbers) are prefixed with `'` so spreadsheets don't run them as formulas.
