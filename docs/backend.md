# Backend

`FormFlow.Backend` is the ASP.NET Core API. For the endpoints themselves see [api.md](api.md). For how it fits with the other projects see [architecture.md](architecture.md).

## Configuration

`appsettings.json`:

| Setting | Default | Purpose |
|---|---|---|
| `ConnectionStrings:LiteDb` | `Filename=formflow.db;Connection=shared` | Where LiteDB stores data |
| `SeedData:DemoSurvey` | `true` | Create the demo survey on first start |
| `DisableHttpsRedirection` | not set | Set to `true` to serve plain HTTP without redirecting, for example behind a proxy that terminates TLS |

Any setting can be overridden on the command line (`dotnet run --SeedData:DemoSurvey=false`) or with environment variables (`SeedData__DemoSurvey=false`).

## Database seeding

`DatabaseSeeder.Seed()` runs once at startup:

1. If the `questions` collection is empty, it loads the 10 sample questions from `SeedData/questions.json`. They cover every question type and include one conditional question (`campus_preference`, shown when `is_student` is yes).
2. If `SeedData:DemoSurvey` is on and the `surveys` collection is empty, it creates the "Student Experience Survey" with every sample question, in the order they appear in the seed file.

To reset, stop the API and delete `formflow.db`. It is recreated and reseeded on the next start.

## Services

- **`SurveyResultsBuilder`** turns a survey's responses into `SurveyResults`: per-option counts for choice, yes/no and single checkbox questions, min/max/average for number questions, and the five most recent answers for text questions. Answers to questions that were hidden count as unanswered.
- **`CsvExporter`** writes one row per response with a column per question key. Values with commas, quotes or line breaks are quoted, and values that a spreadsheet would treat as a formula are prefixed with `'`.

## Error responses

- Question and survey validation errors return `400` with `{ "errors": [...] }` or `{ "error": "..." }`.
- Answer validation errors return `400` as RFC 7807 problem details (`Results.ValidationProblem`) with errors keyed by question key.
- Conflicts (duplicate keys, deleting something that is in use) return `409` with a message saying what depends on the item.

## JSON schemas

`Schemas/question-definition.schema.json` and `Schemas/survey-definition.schema.json` describe the question and survey documents for anyone producing them outside the app, such as a bulk import. The survey schema references the question schema with `$ref`, so both files must stay in the same folder. The API itself validates with `QuestionValidator` and the endpoint checks described in [api.md](api.md).
