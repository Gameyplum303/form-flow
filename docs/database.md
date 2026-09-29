# Database

FormFlow stores everything in [LiteDB](https://www.litedb.org/), an embedded document database. There is no server to install: the data lives in one file, `formflow.db`, next to the API.

## Setup

`Program.cs` registers a single shared `ILiteDatabase`, which is the recommended pattern for LiteDB:

```csharp
builder.Services.AddSingleton<ILiteDatabase>(sp =>
{
    var connectionString = builder.Configuration.GetConnectionString("LiteDb") ?? "Filename=formflow.db;Connection=shared";
    return new LiteDatabase(connectionString);
});
```

The tests replace this registration with an in-memory database (see [testing.md](testing.md)).

## Collections

| Collection | Document | Repository |
|---|---|---|
| `questions` | `QuestionDefinition` | `IQuestionRepository` / `QuestionRepository` |
| `surveys` | `SurveyDefinition` | `ISurveyRepository` / `SurveyRepository` |
| `responses` | `SurveyResponse` (indexed on `SurveyId`) | `IResponseRepository` / `ResponseRepository` |
| `users` | `AdminUser` (username, password hash, role, status, email and whether it is verified, when the password last changed; unique index on `Username`) | `IUserRepository` / `UserRepository` |
| `account_tokens` | `AccountToken` (the SHA-256 hash of an emailed link's token, its account, purpose and expiry; unique index on `TokenHash`) | `IAccountTokenRepository` / `AccountTokenRepository` |

Endpoints and services only use the repository interfaces, never LiteDB directly. That keeps the endpoints easy to test and would let the storage change without touching them.

## Repositories

**`IQuestionRepository`**

| Method | Description |
|---|---|
| `Insert(question)` | Stores a new question and returns it |
| `FindById(id)` | The question, or `null` |
| `FindAll()` | Every question |
| `FindOne(predicate)` | The first question matching a predicate, such as a key lookup |
| `Update(question)` | Replaces a question; `false` if it doesn't exist |
| `Delete(id)` | Deletes a question; `false` if it doesn't exist |

**`ISurveyRepository`**

| Method | Description |
|---|---|
| `Insert(survey)` | Stores a new survey |
| `FindById(id)` | The survey, or `null` |
| `FindAll()` | Every survey |
| `FindByQuestionId(questionId)` | Surveys that include a question, used to block unsafe deletes and key changes |
| `Update(survey)` | Replaces a survey |
| `Delete(id)` | Deletes a survey |

**`IResponseRepository`**

| Method | Description |
|---|---|
| `Insert(response)` | Stores a submitted response |
| `FindBySurveyId(surveyId)` | A survey's responses, oldest first |
| `CountBySurveyId(surveyId)` | How many responses a survey has |
| `DeleteBySurveyId(surveyId)` | Deletes a survey's responses, used when the survey is deleted |

## Stored response shape

```json
{
  "_id": "…",
  "surveyId": "…",
  "submittedAt": "2026-09-29T12:58:00Z",
  "answers": {
    "first_name": ["Ada"],
    "is_student": ["true"],
    "skills": ["csharp", "sql"]
  }
}
```

Every answer is a list of strings, whatever the question type. Answers to hidden questions are never stored.

## Resetting

Stop the API and delete `FormFlow.Backend/formflow.db`. The next start recreates it and loads the sample data.
