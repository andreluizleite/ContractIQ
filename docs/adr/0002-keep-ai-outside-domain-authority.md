# ADR 0002: Keep AI outside domain authority

- Status: Accepted
- Date: 2026-08-27

## Context

ContractIQ uses an LLM to understand questions, retrieve knowledge, explain results, and select application capabilities. LLM output is probabilistic and cannot be the authority for financial calculations or state transitions.

## Decision

Keep cancellation rules, validation, authorization, calculations, idempotency, state transitions, and transactions inside deterministic .NET code. The model may request an application tool, but tools call CQRS handlers and domain logic rather than writing directly to persistence.

Documents retrieved through RAG provide evidence and explanation. Any policy that changes operational behavior must also exist as structured, versioned business data.

## Consequences

- Domain tests do not require a model.
- Tool calls remain safe when model output is incorrect or repeated.
- Answers can distinguish deterministic conclusions from supporting evidence.
- Structured rules and their source documents must be kept consistent.
- Divergence between structured data and document evidence must be reported rather than silently reconciled by the model.
