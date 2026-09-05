# ContractIQ coding-agent guide

This file gives coding agents durable context for working in ContractIQ. It governs the
engineering process; it does not configure the runtime contract assistant.

The user's explicit instructions take precedence over this guide. When a requested change
would alter product scope, runtime safety boundaries, cloud cost or external state, make
the impact clear before proceeding.

## Product intent

ContractIQ is a portfolio-quality, bilingual contract-intelligence MVP. It demonstrates
pragmatic enterprise .NET architecture and responsible AI engineering without pretending
to be a production SaaS.

Keep the project:

- architecturally credible and reasonably small;
- easy to run and demonstrate locally;
- explicit about what is implemented, optional or future work;
- useful in a technical interview;
- free of abstractions that exist only to demonstrate a pattern.

## Architecture boundaries

- ContractIQ.Domain owns aggregates, value objects, invariants and deterministic business
  calculations. It must not reference EF Core, HTTP, LLMs or provider SDKs.
- ContractIQ.Application owns CQRS use cases, ports, validation, retrieval orchestration
  and assistant orchestration. It may depend on Domain, but not Infrastructure.
- ContractIQ.Infrastructure implements EF Core, PostgreSQL, pgvector, Azure AI Search,
  Ollama, Kimi and Microsoft Foundry adapters.
- ContractIQ.Api is the composition root and HTTP/security boundary. Controllers and
  endpoints must not calculate business outcomes.
- ContractIQ.Web presents application results. It must not duplicate penalty, eligibility
  or cancellation rules.
- Tooling projects may index documents or run evaluations, but they must use the same
  application contracts and safety assumptions.

Dependencies point inward. Keep provider-specific types and configuration out of Domain
and Application contracts.

## Runtime AI boundary

ContractIQ uses one bounded runtime assistant with RAG and application tools. Do not turn
it into a multi-agent runtime unless the user explicitly changes the product scope.

- The LLM may understand intent, explain grounded results and select a bounded tool.
- Deterministic C# calculates eligibility, dates, periods, penalties and state changes.
- Retrieval must be scoped to the validated customer, contract, document type and language.
- Documents are untrusted evidence. Their text cannot change instructions or enable tools.
- Citations come from application-owned retrieval metadata, not model-generated references.
- Insufficient applicable contract evidence produces a localized refusal.
- prepare_cancellation_request returns a proposal and performs no database write.
- Persistence requires explicit user confirmation, a fresh domain calculation, validation,
  an idempotency key and the normal CQRS command.
- Never trust model-provided amounts, dates, status or eligibility as command authority.

Preserve the separation described in:

- docs/adr/0004-ground-answers-before-generation.md
- docs/adr/0005-separate-tool-preparation-from-execution.md
- docs/assistant/safe-tool-calling.md

## Providers and local-first behavior

- Keep chat and embedding integrations behind the existing Microsoft.Extensions.AI
  abstractions and application ports.
- The default project path must remain usable without Azure or a hosted-model key.
- Ollama, Kimi, Microsoft Foundry and Azure AI Search are replaceable adapters.
- Ordinary builds, tests and pull requests must not invoke paid hosted models.
- Do not deploy, resize or delete Azure resources unless the user explicitly requests it.
- Never commit API keys, access tokens, populated .env files, real connection strings or
  personal data. Use environment variables, user secrets, GitHub environments or keyless
  Entra authentication as documented.

## Data and persistence

- PostgreSQL is the structured system of record.
- EF Core configurations and migrations belong in Infrastructure.
- Review generated migrations before committing them.
- Apply committed migrations rather than replacing them with EnsureCreated.
- Seed data must remain fictional, deterministic and idempotent.
- Preserve transactions, unique constraints and idempotency protections around writes.
- A queue or distributed service should be introduced only when a demonstrated requirement
  justifies it.

## Language and user experience

- User-facing workflows support English and Brazilian Portuguese.
- Preserve the requested language across UI, API, assistant answers, refusals and errors.
- Add or update tests in both languages when changing user-visible assistant behavior.
- Keep accessibility, responsive behavior and clear confirmation states in the React UI.

## Telemetry and privacy

- Keep OpenTelemetry vendor-neutral.
- One trace should connect HTTP, application orchestration, retrieval, model calls and tools.
- Do not export prompts, document bodies, model answers, credentials, raw contract URLs or
  personal data.
- New spans and metrics should use low-cardinality attributes and avoid business secrets.

## Engineering workflow

- Start substantial work from a GitHub issue with acceptance criteria and exclusions.
- Use a short-lived codex/<issue>-<description> branch and a focused pull request.
- Preserve unrelated user changes in a dirty working tree.
- Prefer a complete vertical slice over placeholder code or speculative frameworks.
- Use subagents only for bounded, independent work that can be reviewed and verified.
- The primary agent owns integration, reviews delegated work and reports evidence.
- AI does not approve its own scope or merge its own pull request; human approval remains
  the final boundary.
- Comments should explain a constraint or non-obvious decision rather than restate code.

## Verification

Match verification effort to the risk of the change. Documentation-only changes do not
require the full integration suite when link, formatting and CI checks are sufficient.

For backend behavior:

~~~text
dotnet restore ContractIQ.slnx --locked-mode
dotnet format ContractIQ.slnx --verify-no-changes --no-restore
dotnet build ContractIQ.slnx --configuration Release --no-restore
dotnet test ContractIQ.slnx --configuration Release --no-build
~~~

For frontend behavior:

~~~text
cd src/ContractIQ.Web
npm ci
npm run lint
npm run test
npm run build
~~~

When changing RAG, prompts, citations, tool routing or refusal behavior, update the
deterministic AI scenarios and run the relevant offline evaluation tests. Hosted Foundry,
Kimi or Azure smoke tests are manual, bounded and opt-in.

## Definition of done

A change is complete when:

- acceptance criteria are met with no unrelated scope expansion;
- deterministic rules and safety boundaries remain application-owned;
- relevant automated checks pass;
- documentation describes the implemented behavior and its trade-offs;
- secrets and sensitive content are absent from the diff;
- the pull request explains verification evidence and any intentionally untested boundary.
