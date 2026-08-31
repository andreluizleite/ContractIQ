# Optional Azure AI implementation plan

## Outcome

ContractIQ will add a cloud profile that demonstrates Microsoft Foundry and Azure AI Search while preserving the zero-cost local profile. Azure remains an adapter choice, never a prerequisite for the deterministic contract workflows.

Planning assumptions for the first personal development environment:

- the Azure subscription was activated on 2026-08-31;
- the promotional USD 200 credit is assumed to expire around 2026-09-30 until the portal shows an authoritative timestamp;
- the project budget target is USD 10, with alerts at USD 5, USD 8, and USD 10;
- paid-capable resources should be reviewed by 2026-09-27 and deleted before the assumed credit expiry when no longer required.

## Runtime profiles

| Profile | Chat | Embeddings | Retrieval | Structured data |
| --- | --- | --- | --- | --- |
| Local | Ollama | Ollama | PostgreSQL lexical + pgvector | PostgreSQL |
| Hosted chat | Kimi | Ollama | PostgreSQL lexical + pgvector | PostgreSQL |
| Azure | Microsoft Foundry | Microsoft Foundry | Azure AI Search BM25 + vector hybrid search | PostgreSQL |

The React application and ASP.NET Core API continue to run locally in the first Azure increment. This prevents a hosting migration from obscuring the AI architecture being demonstrated.

## Architectural boundary

The Azure profile implements existing application ports:

- `IAssistantAnswerGenerator` gets a Foundry-backed `IChatClient` adapter;
- `IKnowledgeEmbeddingGenerator` gets a Foundry embedding adapter;
- `IKnowledgeIndex` and `IKnowledgeSearch` get Azure AI Search adapters.

The domain remains unchanged. Cancellation eligibility, penalty calculation, validation, idempotency, state changes, and transactions remain in .NET and PostgreSQL. The model may choose a bounded tool, but it cannot directly write business state.

ContractIQ does not deploy a second hosted agent in Foundry for this increment. The existing .NET assistant remains the single orchestrator so that its tools, confirmation boundary, telemetry, and tests stay application-owned.

## Delivery slices

### 1. Azure foundation

- validate the subscription, ownership, quota, and provider availability;
- review Bicep with a subscription budget, one resource group, a Foundry account/project, free Azure AI Search, and RBAC;
- run `what-if` and stop for explicit provisioning approval;
- provision no model until its live SKU and quota are verified.

### 2. Foundry adapters

- add a `Foundry` assistant provider without changing the local default;
- authenticate with `DefaultAzureCredential` and the developer's Azure CLI session;
- add a Foundry embedding generator whose dimensions are configuration-owned;
- translate external failures to the existing safe application exceptions;
- record dependency duration and result without prompt or document bodies.

### 3. Azure AI Search adapters

- define a versioned index schema for document identity, scope, version, chunk text, citation metadata, and vectors;
- make indexing idempotent for the same document version;
- execute keyword and vector retrieval in one hybrid request;
- preserve customer and contract filters before ranking;
- map Azure results to application-owned `KnowledgeEvidence` citations.

### 4. CI and live validation

- compile Bicep on ordinary pull requests without Azure authentication;
- run unit and simulated integration tests without hosted calls;
- add a manually dispatched smoke test for a small, bounded Azure query;
- use GitHub OIDC for future automated Azure access instead of a stored client secret;
- never provision or invoke a model automatically on every push.

### 5. Documentation and portfolio proof

- document local and Azure setup, indexing, querying, troubleshooting, cost inspection, and teardown;
- capture a cited ACME answer and correlated Aspire trace;
- demonstrate that the confirmed cancellation request still executes CQRS/domain logic;
- update the interview guide, architecture diagram, release notes, and CV only after the live flow is verified.

## Acceptance criteria

- Local startup, tests, and demo do not contact Azure.
- Azure configuration contains no committed key or secret.
- Azure AI Search uses the free SKU for the portfolio environment.
- Model deployments are selected from live catalog and quota data rather than hardcoded assumptions.
- Hybrid results are scoped, grounded, and mapped to citations.
- A hosted model cannot bypass confirmation or deterministic domain rules.
- Normal PR CI performs no billable call.
- Telemetry records provider, operation, duration, and outcome without sensitive content.
- Teardown is documented and verified before the assumed credit expiry.

## Explicitly deferred

- hosting the API, React application, or PostgreSQL in Azure;
- private networking and private endpoints;
- a hosted Foundry agent or multi-agent architecture;
- production tenant isolation and paid Search tiers;
- continuous live LLM evaluation;
- Microsoft Entra authentication for end users, which remains a separate M5 delivery.
