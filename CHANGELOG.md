# Changelog

## [Unreleased]

### Added
- Survey sharing. New surveys start as drafts that only their owner (and administrators) can open. The Blazor **Share** page publishes a survey, chooses whether it also shows on the public list or is reachable by its link only, sets an optional close date, and shows the share link (`/s/{code}`) with a copy button and a QR code. The survey list shows each survey's status. The React app opens share links as `#/s/{code}`. API: `PUT /api/surveys/{id}/sharing`, `GET /api/share/{code}`, and `GET /api/surveys/{id}/answered`. Surveys stored before this change stay published and listed.
- One answer per browser. Respondents still have no account; each browser keeps a random respondent id that is sent with its answers, and the API returns 409 for a second answer, or for any answer once the survey has closed. Coming back to a survey shows a thank-you instead of the form.
- Three roles: Administrator, Professor/Scientist and Student. Administrators manage every question and survey. Professors create questions and surveys and edit, delete and see results for only the ones they created, and can use anyone's questions in their surveys. Students take surveys. Questions and surveys record their creator, `GET /api/surveys/managed` lists the surveys a caller can manage, and responses from a signed-in account record who sent them (a `submitted_by` CSV column). Development and docker compose add a `professor` test account.
- Professor/scientist sign-up at `/signup` (name, email, password, date of birth, organization, and how they'll use FormFlow). New accounts wait until an administrator approves them on the Blazor **Sign-ups** page (`GET /api/accounts/pending`, `POST /api/accounts/{id}/approve` and `/decline`); set `SignUp:RequireApproval` to `false` to skip approval. Students take surveys without an account, so there is no student test account.
- Administrators can view the Blazor site as a professor or a student with **View as** in the menu, and switch back from a banner.
- Four question types: `long_text` (a paragraph box), `email`, `date` and `rating` (1 to 5 stars, or up to 10). The server validates each one, both front ends render them, results show a star count and average rating, and the demo survey has an example of each.
- Admin sign-in. `POST /api/auth/login` issues a JWT, passwords are hashed with ASP.NET Core Identity's `PasswordHasher`, and each account has a role. Accounts listed under `Accounts` are created at startup; Development and docker compose create `Rogers` (administrator) and the test account `professor`.
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
- The Blazor preview page now sends the sign-in with its requests, so owners can preview their drafts.
- Blazor: a text question now shows a new server error after someone edits the field. Before, the second error (for example a bad email after a missing one) was hidden.
- Parallel API tests could fail with "Member … not found on BsonMapper" because LiteDB's shared mapper was built by several threads at once. Mappings are now built once before first use.
- Radio and checkbox questions in Blazor didn't report their answers.
- CI never ran on pull requests; `dotnet format` is now checked too.

### Changed
- The public survey list (`GET /api/surveys`) only shows published, listed surveys that haven't closed. The take-survey page no longer has a "Submit another response" button.
- The Blazor results page downloads the CSV through the signed-in session instead of linking to the API.
- Updated packages with known vulnerabilities (Microsoft.AspNetCore.OpenApi, bUnit and their dependencies).
- The React `VisibleIf` type now matches the API (`key`, `shouldEqual`).

### Removed
- Empty Flutter, MAUI and React Native placeholder folders, unused classes and duplicate validation code.

## Course project (February to April 2026)

The state at the end of the ECU Software Engineering course, as in [ECU-Pirate-Forge/form-flow](https://github.com/ECU-Pirate-Forge/form-flow): question bank with seven question types, JSON seed data, question and survey creation in Blazor, and a React renderer.
