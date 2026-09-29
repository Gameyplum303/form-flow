# Changelog

## [Unreleased]

### Added
- Survey responses: `POST /api/surveys/{id}/responses` validates every answer on the server (required, types, option values, validation rules, conditional visibility) and returns RFC 7807 problem details keyed by question.
- Results (`GET /api/surveys/{id}/results`) and CSV export (`GET /api/surveys/{id}/responses/export`).
- Edit and delete for questions and surveys, with guards for questions that surveys or visibility rules depend on.
- `GET /api/surveys/{id}/questions` to load a survey's questions in order.
- OpenAPI document and Swagger UI.
- A demo survey seeded from the sample questions.
- Blazor: survey list, take-survey page, results page, question and survey editing, and delete with confirmation.
- React: survey list and take-survey flow against the API, with real controls for every question type and conditional questions.
- Validation rules and "only show when" settings in the admin question editor.

### Fixed
- Radio and checkbox questions in Blazor didn't report their answers.
- CI never ran on pull requests; `dotnet format` is now checked too.

### Changed
- Updated packages with known vulnerabilities (Microsoft.AspNetCore.OpenApi, bUnit and their dependencies).
- The React `VisibleIf` type now matches the API (`key`, `shouldEqual`).

### Removed
- Empty Flutter, MAUI and React Native placeholder folders, unused classes and duplicate validation code.

## Course project (February to April 2026)

The state at the end of the ECU Software Engineering course, as in [ECU-Pirate-Forge/form-flow](https://github.com/ECU-Pirate-Forge/form-flow): question bank with seven question types, JSON seed data, question and survey creation in Blazor, and a React renderer.
