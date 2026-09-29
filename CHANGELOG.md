# Changelog

## [Unreleased]

### Added
- Admin sign-in. `POST /api/auth/login` issues a JWT, passwords are hashed with ASP.NET Core Identity's `PasswordHasher`, and every endpoint that changes questions or surveys or reads responses requires the admin role. The first admin account comes from `Admin:Username` and `Admin:Password`.
- Rate limits per IP address on sign-in and survey submissions.
- Blazor sign-in page, a guard that sends signed-out visitors from `/admin` pages to it, and Sign out in the admin menu. The token is kept per tab in encrypted session storage.
- Swagger UI shows which endpoints need a token and has an Authorize button.
- Dockerfiles for the API, Blazor and React apps, and `docker-compose.yml` to run all three with one command.
- Playwright browser tests (`FormFlow.E2E`) that CI runs against the Docker images.
- Code coverage in CI, with a report on each run and a README badge.
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
- Parallel API tests could fail with "Member … not found on BsonMapper" because LiteDB's shared mapper was built by several threads at once. Mappings are now built once before first use.
- Radio and checkbox questions in Blazor didn't report their answers.
- CI never ran on pull requests; `dotnet format` is now checked too.

### Changed
- The Blazor results page downloads the CSV through the signed-in session instead of linking to the API.
- Updated packages with known vulnerabilities (Microsoft.AspNetCore.OpenApi, bUnit and their dependencies).
- The React `VisibleIf` type now matches the API (`key`, `shouldEqual`).

### Removed
- Empty Flutter, MAUI and React Native placeholder folders, unused classes and duplicate validation code.

## Course project (February to April 2026)

The state at the end of the ECU Software Engineering course, as in [ECU-Pirate-Forge/form-flow](https://github.com/ECU-Pirate-Forge/form-flow): question bank with seven question types, JSON seed data, question and survey creation in Blazor, and a React renderer.
