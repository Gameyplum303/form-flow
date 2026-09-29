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
| `Endpoints/` | `QuestionEndpoints`, `SurveyEndpoints`, `ResponseEndpoints` |
| `Repositories/` | LiteDB repositories for questions, surveys and responses |
| `Services/` | `SurveyResultsBuilder` and `CsvExporter` |
| `SeedData/` | The sample questions loaded into an empty database |
| `Schemas/` | JSON schemas for question and survey documents |
