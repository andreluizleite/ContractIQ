# Safe assistant tool calling

ContractIQ uses one assistant with RAG and application tools. It is not a multi-agent system. The model may decide that a tool is useful, but tool scope, confirmation, validation, idempotency, transactions, and persistence remain controlled by the .NET application.

## Demonstration flow

After selecting ACME and its active contract, ask:

```text
Create the cancellation request.
```

The agent calls `prepare_cancellation_request`. The tool recalculates a deterministic preview and returns an action proposal containing the effective date and penalty. No row is created at this point.

The React experience then shows **Action prepared by the agent**. Selecting **Review and confirm action** opens a confirmation dialog. Only after the user reviews the preview, checks the confirmation box, and submits does the frontend call the write endpoint with a new idempotency key.

## Tools available to the model

The local Ollama adapter uses Microsoft `IChatClient`, `AIFunctionFactory`, and `FunctionInvokingChatClient` to expose four functions:

| Tool | Capability | Changes state |
| --- | --- | --- |
| `get_selected_contract` | Reads structured details for the selected contract. | No |
| `assess_selected_contract_cancellation` | Runs deterministic domain assessment. | No |
| `search_selected_contract_evidence` | Runs scoped hybrid retrieval for the current question. | No |
| `prepare_cancellation_request` | Returns a deterministic action preview. | No |

Each function closes over the customer and contract selected and validated by the API. The model cannot substitute another identifier through a generated function argument.

## Write boundary

The write capability is deliberately not included in the automatically invocable model tool list.

`POST /api/v1/assistant/actions/cancellation-requests` requires:

- customer and contract identifiers;
- the exact `create_cancellation_request` intent;
- `confirmed: true` from the user interaction;
- an `Idempotency-Key` header.

The endpoint accepts no date, penalty, status, eligibility, or amount from the model or browser. A focused application transaction encloses customer-scope verification, the existing `CreateCancellationRequestHandler`, deterministic recalculation, validation, and PostgreSQL persistence. Unique constraints and conflict handling additionally protect concurrent idempotency and open-request rules.

Missing confirmation returns `400`. A contract outside the customer scope returns `404`. Replaying the same key returns the original request with `200`; a different key for an already open request returns `409`.

## Audit and privacy

Every tool outcome emits a structured log event with:

- event identifier;
- tool name;
- customer and contract identifiers;
- outcome;
- whether it changes state;
- UTC timestamp.

The event never contains the user question, prompt, generated answer, retrieved evidence, or contract document content. This gives the portfolio demo an auditable boundary without creating a new audit database prematurely.

## Safety trade-off

Microsoft.Extensions.AI also supports approval-required functions. ContractIQ keeps the write endpoint outside the first model turn instead of serializing provider conversation state between HTTP requests. This produces a small, explicit, provider-neutral confirmation contract that is easy to test locally and can later be mapped to a hosted agent approval protocol.
