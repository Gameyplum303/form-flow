# Agent Instructions

Guidance for AI coding assistants working in this repository.

## Project

FormFlow is a survey builder. `FormFlow.Data` holds shared models and validation, `FormFlow.Backend` is an ASP.NET Core minimal API over LiteDB, `FormFlow.Blazor` is the admin and respondent web app, and `FormFlow.React` is a respondent client. Read [docs/architecture.md](docs/architecture.md) first.

## Commands

```bash
dotnet build FormFlow.slnx
dotnet test FormFlow.slnx
dotnet format FormFlow.slnx --verify-no-changes
cd FormFlow.React && npm run lint && npm run build
cd FormFlow.React.Tests && npm test
```

All of these must pass before a change is pushed; CI runs the same set.

## Rules

- Keep changes small and focused, with tests.
- Validate answers on the server; clients display the API's errors.
- Keep `FormFlow.Data/Services/VisibilityEvaluator.cs` and `FormFlow.React/src/logic/visibility.ts` in sync.
- Endpoints use repository interfaces, never LiteDB directly.
- Update the relevant page in `docs/` when behavior changes.
- Don't add new frameworks or large refactors without asking.
