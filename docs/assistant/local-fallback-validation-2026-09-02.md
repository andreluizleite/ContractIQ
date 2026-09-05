# Local fallback and offline AI validation — 2026-09-02

## Outcome

The ContractIQ local fallback passed without calling Microsoft Foundry, Azure
AI Search, Kimi, OpenAI, or Ollama. A model-provider outage does not block the
application's deterministic contract assessment or cancellation command.

## Reproducible evaluation result

The versioned offline evaluator ran the real application handlers and domain
rules with deterministic in-memory adapters.

| Field | Recorded value |
| --- | --- |
| Result | PASS |
| Scenarios | 12/12 |
| Critical failures | 0 |
| Provider | `deterministic-baseline` |
| Deployment | `deterministic-baseline-v2` |
| Observed model id | `deterministic-baseline-v2` |
| Prompt version | `grounded-answer-v1` |
| Dataset | `ContractIQ bilingual grounding and safe-action evaluation set` |
| Dataset schema | `contract-assistant-v1` |
| Run date (UTC) | `2026-09-02T20:07:46.1163005+00:00` |

The twelve scenarios cover English and Brazilian Portuguese, penalty and
no-penalty results, insufficient evidence, action preparation, explicit
confirmation, prompt injection, document/domain conflict, and an inactive
contract. Every deterministic safety gate passed.

Run the same free evaluation with:

```powershell
dotnet run --project tools/ContractIQ.AiEvaluator -- `
  --mode offline `
  --output TestResults/ai-evaluations/local-fallback
```

The generated `report.json` and `report.md` are sanitized and intentionally stay
under the ignored `TestResults` directory. They contain no prompts, answers,
document text, customer text, credentials, or access tokens.

## Provider-outage proof

The API integration test replaces the model adapter with one that always
returns a controlled dependency-unavailable error. In the same test:

1. the assistant endpoint returns HTTP 503;
2. the deterministic cancellation assessment still returns HTTP 200;
3. the direct CQRS cancellation command still returns HTTP 201.

The React test independently simulates the same assistant 503. It verifies that
the UI displays a recoverable AI warning, keeps the deterministic penalty
visible, and leaves the direct cancellation action enabled.

Run the focused proof with Docker available for the temporary PostgreSQL test
container:

```powershell
dotnet test tests/ContractIQ.IntegrationTests/ContractIQ.IntegrationTests.csproj `
  -m:1 `
  --disable-build-servers `
  --filter "FullyQualifiedName~Assistant_outage_does_not_block_deterministic_contract_operations"
```

The validated run passed 1/1 integration test. The complete deterministic AI
evaluation test project also passed 15/15 tests.

## What this evidence does and does not prove

It proves the local operational fallback, application orchestration, domain
authority, citation metadata, bilingual safety signals, and confirmation
boundary without a network dependency or inference charge.

It does not claim to measure the prose quality of a live LLM. A live Foundry
observation remains manual, non-blocking, and subject to a separately approved
request ceiling documented in [AI evaluations](ai-evaluations.md).
