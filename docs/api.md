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

Reading questions and surveys and submitting answers are public. Everything that changes the question bank or surveys, and everything that reads responses, needs an admin token. Those endpoints are marked **Admin** below and return `401` without a valid token.

### `POST /api/auth/login`

```json
{ "username": "admin", "password": "formflow-admin" }
```

| Status | When |
|---|---|
| 200 | `{ "token": "eyJ…", "username": "admin", "expiresAt": "2026-09-29T20:00:00Z" }` |
| 401 | Problem details titled "Invalid username or password." The same answer is given for an unknown user and a wrong password, and both take the same time. |
| 429 | More than `RateLimits:LoginPerMinute` attempts from one IP address in a minute |

Send the token on admin requests:

```
Authorization: Bearer eyJ…
```

Tokens are signed JWTs with the `admin` role and last `Jwt:LifetimeMinutes` (8 hours by default). In Swagger UI, sign in with the login endpoint, then paste the token into **Authorize**.

### `GET /api/auth/me` (Admin)

Returns `{ "username": "admin" }` for the token's user, so a client can check that its token is still valid.

### Admin accounts

Passwords are stored as salted PBKDF2 hashes (ASP.NET Core Identity's `PasswordHasher`) in the `users` collection. On first start the API creates one admin from `Admin:Username` and `Admin:Password` if the collection is empty. See [backend.md](backend.md#configuration).

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

### `POST /api/questions` (Admin)

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
| 201 | Created. Body is the stored question with its new `id`. |
| 400 | `{ "errors": ["..."] }` listing every rule the question breaks (see below) |
| 409 | A question with that `key` already exists |

Rules checked on create and update:

- `key`, `label` and `type` are required, and `type` must be one of `text`, `number`, `yes_no`, `dropdown`, `radio`, `checkbox`, `multiselect`.
- `dropdown`, `radio` and `multiselect` need at least one option. A `checkbox` with no options is a single tick box.
- Option labels and values are required and must be unique within the question.
- `visibleIf.key` must name an existing `yes_no` question other than this one.
- `validationConfigs`, when present, must be a JSON array of rules with a known `validationType` (`MinLength`, `MaxLength`, `MinValue`, `MaxValue`, `Range`). See [question-definition.md](question-definition.md).

### `PUT /api/questions/{id}` (Admin)

Same body and rules as `POST`.

| Status | When |
|---|---|
| 200 | Updated question |
| 400 | `{ "errors": [...] }` |
| 404 | No question with that id |
| 409 | `{ "error": "..." }` when the new key belongs to another question, or when the key changes while a survey or another question's `visibleIf` still uses it. Stored answers are keyed by question key, so a key in use stays fixed. |

### `DELETE /api/questions/{id}` (Admin)

| Status | When |
|---|---|
| 204 | Deleted |
| 404 | No question with that id |
| 409 | `{ "error": "..." }` naming the surveys that use it, or the questions whose `visibleIf` depends on it |

---

## Surveys

A survey is a title, a description and an ordered list of question ids.

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
| `GET` | `/api/surveys` | Public | All surveys |
| `GET` | `/api/surveys/{id}` | Public | One survey; 400 if `id` is not a GUID, 404 if missing |
| `GET` | `/api/surveys/{id}/questions` | Public | The survey's questions in survey order, so a client can render it with one call; 404 if missing |
| `POST` | `/api/surveys` | Admin | 201 with the stored survey, or 400 `{ "error": "..." }` |
| `PUT` | `/api/surveys/{id}` | Admin | 200 with the updated survey (keeps `createdAt`), 400, or 404 |
| `DELETE` | `/api/surveys/{id}` | Admin | 204, also deleting the survey's responses; 404 if missing |

A survey is rejected with 400 when the title or description is empty, when it has no questions, when a question appears twice, or when a question id doesn't exist.

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
  }
}
```

The server validates the whole submission against the survey's questions:

- Answers for hidden questions (their `visibleIf` rule is not met) are dropped, and hidden questions are never required.
- Required visible questions must have an answer.
- Unknown keys are rejected.
- `number` answers must be numbers, `yes_no` answers must be `true`/`false` (or `yes`/`no`), and choice answers must be one of the question's option values with no duplicates.
- Text and number answers are checked against the question's `validationConfigs` rules.

| Status | When |
|---|---|
| 201 | Stored. Body is the saved response with normalized answers. |
| 400 | RFC 7807 validation problem, with one entry per question key |
| 404 | No survey with that id |
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

### `GET /api/surveys/{id}/responses` (Admin)

All stored responses for the survey, oldest first. 404 if the survey doesn't exist.

### `GET /api/surveys/{id}/results` (Admin)

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

### `GET /api/surveys/{id}/responses/export` (Admin)

Downloads every response as `<survey-title>-responses.csv`. Columns are `response_id`, `submitted_at`, then one column per question key in survey order. Multiple values are joined with `; `. Cells that start with `=`, `+`, `-`, `@`, a tab or a carriage return (and aren't numbers) are prefixed with `'` so spreadsheets don't run them as formulas.
