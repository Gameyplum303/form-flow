# Contributing

## Setup

You need the .NET 10 SDK and Node.js 20 or later. See [Run it locally](README.md#run-it-locally) to start the API and the two front ends.

## Workflow

1. Branch from `dev`.
2. Keep each change focused, and add or update tests with it.
3. Run the checks below.
4. Open a pull request into `dev`. CI must pass before it is merged.

## Checks

These are the same checks CI runs:

```bash
dotnet build FormFlow.slnx
dotnet test FormFlow.slnx
dotnet format FormFlow.slnx --verify-no-changes   # run without --verify-no-changes to fix

cd FormFlow.React && npm ci && npm run lint && npm run build
cd ../FormFlow.React.Tests && npm ci && npm test
```

## Conventions

- API endpoints go in `FormFlow.Backend/Endpoints/`, one static class per resource, and reach the database only through a repository interface.
- Rules that both the API and a client need (question types, visibility) live in `FormFlow.Data`. If you change visibility rules, change `FormFlow.React/src/logic/visibility.ts` to match.
- Answer validation happens on the server. Clients show the errors the API returns instead of duplicating the rules.
- Update the matching page in `docs/` when you change behavior, and add a line to `CHANGELOG.md`.
- Write commit messages in the imperative: "Add CSV export", not "Added CSV export".

Please follow the [Code of Conduct](CODE_OF_CONDUCT.md).
