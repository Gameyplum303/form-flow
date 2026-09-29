# Changelog

## [Unreleased]

### Added
- Survey templates. The Blazor **Create Survey** page opens on a gallery of ready-made surveys (Course evaluation, Event feedback, Research study intake with a consent question and a conditional follow-up, and Customer satisfaction) and a **Start blank** option. **Use** makes a draft copy to edit and publish. Templates are seeded at startup (`SeedData/templates.json`, skipped when they already exist, off with `SeedData:Templates`), are read-only, and never show on survey lists or take answers. API: `GET /api/templates` and `POST /api/templates/{id}/use`.
- **Duplicate** on the Blazor survey list copies a survey into a new draft named "Copy of …" with the same questions and pages, a new share link, no responses and no close date. API: `POST /api/surveys/{id}/duplicate`.
- Pages. A survey's `pageBreaks` lists the questions that start a new page, set with the **New page starts here** switch in the survey editor. Respondents in Blazor and React see one page at a time with "Page 2 of 3", a progress bar, **Back** and **Next**, and **Submit** on the last page. Next checks the page's required answers first, pages whose questions are all hidden are skipped, and a rejected submission opens the first page with an error. The preview is paged too. Surveys without page breaks look as before.
- Saved answers. Both front ends keep a respondent's answers in the browser as they go, bring them back with "We saved your answers on this device." and a **Start over** link, and clear them once the answers are sent.
- Three question types. `likert` rates several statements (the new `rows` property) on one shared scale, the question's options or Strongly disagree (1) to Strongly agree (5) by default; answers are stored as `row=option`, and a required grid needs every row answered. `nps` asks "How likely are you to recommend…" on eleven buttons from 0 to 10. `slider` picks a whole number between the question's minimum and maximum value (0 to 100 by default) and stays unanswered until it is moved. The server validates each one, both front ends render them accessibly (a grid row is a radio group labelled by its statement, and the grid scrolls sideways on small screens), and the question editor has a rows editor for grids. Results show each statement's counts and average, the NPS with promoters, passives and detractors, and a slider's average, minimum and maximum; NPS questions can filter and split results, and group comparisons include NPS and statement averages. The CSV export gives each grid row its own `key[row]` column. The demo survey has an optional example of each (16 questions), and sample responses answer them.
- A public demo setup: `render.yaml` deploys the API, the Blazor app and the React app to Render's free plan, with public demo accounts and a README button. See docs/deployment.md.
- `SeedData:SampleResponses` fills the demo survey with made-up but valid responses spread over the last three weeks, so a fresh demo has results, comparisons and a timeline to show.
- Survey analytics on the results page. Filter to responses that gave certain answers, keep a date range, and compare groups side by side, with a bar chart of responses per day (week or month over long ranges) in the viewer's time zone. The CSV download follows the same filters. API: `filter`, `from`, `to`, `compareBy` and `utcOffset` query parameters on `GET /api/surveys/{id}/results`, and `filter`, `from` and `to` on the export; results now include `matchingResponses`, `timeline` and `comparison`.
- Account security. Professors verify their email address from an emailed link before they can sign in, and can ask for a new link from the sign-in page. **Forgot your password?** emails a reset link that lasts one hour and works once, and **Change password** (`/account`) changes it while signed in. Both reset and change sign the account out of every other session, and deleting an account ends its sessions too. Links carry a random token of which only a SHA-256 hash is stored. API: `POST /api/auth/verify-email`, `/resend-verification`, `/forgot-password`, `/reset-password` and `/change-password`; sign-in's 403 now carries a `reason` (`email_unverified` or `pending`). Set `SignUp:RequireEmailVerification` to `false` to skip verification.
- Email through any SMTP service (`Email:Smtp:*`, or `FORMFLOW_SMTP_*` in docker compose). Without one, emails stay in an in-memory outbox that administrators read on the Blazor **Emails** page (`GET /api/accounts/outbox`), so the demo and the browser tests work without an email account. Configured accounts can have an `Email` (`FORMFLOW_ADMIN_EMAIL`) so they can reset their password too.
- Survey sharing. New surveys start as drafts that only their owner (and administrators) can open. The Blazor **Share** page publishes a survey, chooses whether it also shows on the public list or is reachable by its link only, sets an optional close date, and shows the share link (`/s/{code}`) with a copy button and a QR code. The survey list shows each survey's status. The React app opens share links as `#/s/{code}`. API: `PUT /api/surveys/{id}/sharing`, `GET /api/share/{code}`, and `GET /api/surveys/{id}/answered`. Surveys stored before this change stay published and listed.
- One answer per browser. Respondents still have no account; each browser keeps a random respondent id that is sent with its answers, and the API returns 409 for a second answer, or for any answer once the survey has closed. Coming back to a survey shows a thank-you instead of the form.
- Three roles: Administrator, Professor/Scientist and Student. Administrators manage every question and survey. Professors create questions and surveys and edit, delete and see results for only the ones they created, and can use anyone's questions in their surveys. Students take surveys. Questions and surveys record their creator, `GET /api/surveys/managed` lists the surveys a caller can manage, and responses from a signed-in account record who sent them (a `submitted_by` CSV column). Development and docker compose add a `professor` test account.
- Professor/scientist sign-up at `/signup` (name, email, password, date of birth, organization, and how they'll use FormFlow). New accounts wait until an administrator approves them on the Blazor **Sign-ups** page (`GET /api/accounts/pending`, `POST /api/accounts/{id}/approve` and `/decline`); set `SignUp:RequireApproval` to `false` to skip approval. Students take surveys without an account, so there is no student test account.
- Administrators can view the Blazor site as a professor or a student with **View as** in the menu, and switch back from a banner.
- Four question types: `long_text` (a paragraph box), `email`, `date` and `rating` (1 to 5 stars, or up to 10). The server validates each one, both front ends render them, results show a star count and average rating, and the demo survey has an example of each.
- Admin sign-in. `POST /api/auth/login` issues a JWT, passwords are hashed with ASP.NET Core Identity's `PasswordHasher`, and each account has a role. Accounts listed under `Accounts` are created at startup; Development and docker compose create `Rogers` (administrator) and the test account `professor`.
- Rate limits per IP address on sign-in, account requests (sign-up, email links and password changes, `RateLimits:AccountPerMinute`) and survey submissions.
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
- The question editor no longer shows its form for a question that doesn't exist, and the survey and question editors can't save twice from a double click.
- A validation rule with a non-numeric value (for example `"maxValue": "10"`) was saved and then made every submission fail with a 500. Rules are now checked when they are saved, and value rules accept decimals.
- Blazor pages show a friendly message when the API can't be reached, instead of the error bar or "No questions found".
- React: opening a second survey no longer shows the first survey's errors.
- The Blazor preview page now sends the sign-in with its requests, so owners can preview their drafts.
- Blazor: a text question now shows a new server error after someone edits the field. Before, the second error (for example a bad email after a missing one) was hidden.
- Parallel API tests could fail with "Member … not found on BsonMapper" because LiteDB's shared mapper was built by several threads at once. Mappings are now built once before first use.
- Radio and checkbox questions in Blazor didn't report their answers.
- CI never ran on pull requests; `dotnet format` is now checked too.

### Changed
- One look across the Blazor app: Roboto everywhere (Bootstrap is gone), one page title style, outlined inputs, the same card for every question type, one sidebar menu whose sign-in and account links match the other links, readable question type names, and real links for Edit, Share, Preview and Results. The React app uses the same font and is named FormFlow.
- The API no longer shows who created a survey or question to people who aren't signed in as an administrator or professor, since a professor's username is their email address.
- CORS: outside Development, the API only accepts browser requests from the origins in `Cors:AllowedOrigins` (render.yaml and docker compose list the React app).
- Unhandled API errors are logged and returned as problem details. Question and survey ids in routes must be GUIDs (a malformed id is a 404).
- Code review cleanup: one ownership check and one API error helper instead of copies, constants instead of repeated strings, the injected clock everywhere, logging instead of console output, consistent naming, and summaries on public types. CI uses a read-only token except for the coverage badge job, cancels superseded runs and caches NuGet packages.
- The public survey list (`GET /api/surveys`) only shows published, listed surveys that haven't closed. The take-survey page no longer has a "Submit another response" button.
- The Blazor results page downloads the CSV through the signed-in session instead of linking to the API.
- Updated packages with known vulnerabilities (Microsoft.AspNetCore.OpenApi, bUnit and their dependencies).
- The React `VisibleIf` type now matches the API (`key`, `shouldEqual`).

### Removed
- Leftover template files: Create React App boilerplate and unused test libraries in the React app, the course's unfilled Copilot and skills templates, an unused sample JSON file, and tests that exercised a copy of the survey editor's logic instead of the page.
- Empty Flutter, MAUI and React Native placeholder folders, unused classes and duplicate validation code.

## Course project (February to April 2026)

The state at the end of the ECU Software Engineering course, as in [ECU-Pirate-Forge/form-flow](https://github.com/ECU-Pirate-Forge/form-flow): question bank with seven question types, JSON seed data, question and survey creation in Blazor, and a React renderer.
