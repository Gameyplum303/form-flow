# Changelog

## [Unreleased]

### Added
- Four question types: `long_text` (a paragraph box), `email`, `date` and `rating` (1 to 5 stars, or up to 10). The server validates each one, both front ends render them, results show a star count and average rating, and the demo survey has an example of each.
- Admin sign-in. `POST /api/auth/login` issues a JWT, passwords are hashed with ASP.NET Core Identity's `PasswordHasher`, and every account is an admin or view-only. Changing questions or surveys needs an admin; reading responses and results needs any account. Accounts listed under `Accounts` are created at startup; Development and docker compose create `Rogers` (admin) and the view-only test account `student`.
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
- Blazor: a text question now shows a new server error after someone edits the field. Before, the second error (for example a bad email after a missing one) was hidden.
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
