# FormFlow.React.Tests

Jest and React Testing Library tests for [`FormFlow.React`](../FormFlow.React). They import the app's source directly from `../FormFlow.React/src`.

```bash
npm install
npm test
```

| Folder | Tests |
|---|---|
| `Components/` | `QuestionRenderer`, `SurveyForm`, and `App` against a mocked `fetch` |
| `Logic/` | The visibility rules in `logic/visibility.ts` |

See [docs/testing.md](../docs/testing.md#how-the-react-tests-work) for how the setup works.
