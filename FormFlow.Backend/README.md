# FormFlow.Backend

The FormFlow REST API: ASP.NET Core minimal APIs over LiteDB.

```bash
dotnet run    # http://localhost:5164 and https://localhost:7209
```

- Swagger UI: http://localhost:5164/swagger
- Example requests: [`backend.http`](backend.http)
- Endpoint reference: [docs/api.md](../docs/api.md)
- Configuration, seeding and services: [docs/backend.md](../docs/backend.md)
- Repositories and collections: [docs/database.md](../docs/database.md)

| Folder | Contents |
|---|---|
| `Endpoints/` | `AuthEndpoints` (`/api/auth`), `AccountEndpoints` (`/api/accounts`), `QuestionEndpoints` (`/api/questions`), `SurveyEndpoints` (`/api/surveys`, `/api/share/{code}`) and `ResponseEndpoints` (`/api/surveys/{id}/responses`, `/results`, `/responses/export`) |
| `Auth/` | JWT settings and tokens, `CurrentUser` ownership checks, accounts and the `AdminAccountSeeder` |
| `Email/` | `AccountEmails` and the SMTP and in-memory outbox senders |
| `Repositories/` | LiteDB repositories for questions, surveys, responses, users and account tokens |
| `Services/` | `SurveyResultsBuilder`, `ResultsQueryParser`, `CsvExporter` and `SampleResponseGenerator` |
| `SeedData/` | The sample questions loaded into an empty database (by `DatabaseSeeder.cs`) |
| `Schemas/` | JSON schemas for question and survey documents |
