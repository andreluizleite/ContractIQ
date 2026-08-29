# ADR 0005: Separate tool preparation from execution

- Status: Accepted
- Date: 2026-08-28

## Context

The assistant must support a request such as "Create the cancellation request" without allowing a model-generated tool call to bypass domain rules or human approval. HTTP requests are stateless, and persisting provider-specific conversation or approval payloads would add unnecessary MVP complexity.

## Decision

Expose scoped read and preparation functions to the model through Microsoft.Extensions.AI. Preparation returns a deterministic preview and cannot change state.

Keep the cancellation write tool outside automatic model invocation. Execute it only through a separate endpoint that requires explicit confirmation and an idempotency key. The tool accepts identifiers and intent, then delegates to the existing CQRS command, which reloads and recalculates all business values.

Record structured tool outcomes without prompts or document content.

## Consequences

The user sees a clear human-in-the-loop boundary, retries are safe, and tests do not depend on a live model. The application does not need to persist an Ollama or Foundry conversation merely to approve a write.

The interaction uses two HTTP requests and the model does not receive the final write result in the same conversational turn. A future hosted-agent profile may use provider-native approval messages while preserving the same application command and confirmation rules.
