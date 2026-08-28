# ADR 0004: Ground answers before model generation

- Status: Accepted
- Date: 2026-08-28

## Context

ContractIQ must answer bilingual contract questions while preserving deterministic business authority and precise citations. Sending only a user question to a language model would allow unsupported answers, model-calculated penalties, cross-customer leakage, and invented references.

## Decision

The application orchestrates grounding before invoking a chat model:

- validate the customer and contract relationship;
- calculate the current cancellation assessment in the domain model;
- retrieve evidence filtered to the same customer, contract, and effective date;
- require an applicable contract clause before generation;
- treat the question and retrieved document text as untrusted input;
- give the model a read-only prompt containing the assessment and numbered evidence;
- return citation metadata assembled by application code;
- use an application port implemented locally through `IChatClient` and Ollama.

The assistant endpoint does not expose tools or perform state changes. Write-capable tool calling requires a separate confirmation design.

## Consequences

Answers remain explainable and the deterministic assessment can be inspected separately from model prose. Tests substitute the answer generator and embeddings, so they cover orchestration, languages, citations, and refusal without downloading a model.

The assistant needs indexed contract evidence and a configured chat model for generated answers. Retrieval quality and model quality still affect usefulness, and prompt injection cannot be eliminated solely through prompting. Production readiness therefore requires evaluation datasets, safe telemetry, and provider-specific safeguards in later milestones.
