# Azure AI live validation — 2026-09-02

## Outcome

The optional Azure profile was exercised end to end from the local ASP.NET Core
application. Microsoft Foundry generated embeddings and the assistant answer,
Azure AI Search performed scoped hybrid retrieval, and PostgreSQL remained the
system of record for structured data and cancellation requests.

The validation did not host the API, React application, or PostgreSQL in Azure.
The default local profile remains independent from Azure.

## Environment

- Foundry chat deployment: `contractiq-chat`, `gpt-5-mini` 2025-08-07,
  GlobalStandard, 10K TPM;
- Foundry embedding deployment: `contractiq-embeddings`,
  `text-embedding-3-small` v1, GlobalStandard, 1K TPM;
- Azure AI Search: `srch-contractiq-dev-7ip435`, Free;
- primary knowledge index: `contractiq-knowledge-v1`, 22 fictional chunks;
- authentication: Microsoft Entra through `AzureCliCredential`, with no local
  service key;
- Foundry and Search SDK retries: zero for the bounded validation;
- telemetry backend: local .NET Aspire Dashboard through OTLP.

## Correlated evidence

### Indexing

Trace `cb0b47f9f3871283e98b69e0d900712f` contains 57 spans in one tree:

```text
contractiq.knowledge.index
└── contractiq.knowledge.document.index (five documents)
    ├── contractiq.knowledge.index.check
    │   └── Azure AI Search version check
    ├── contractiq.knowledge.embedding.generate
    │   └── contractiq.ai.embedding.request
    │       └── Foundry HTTP request
    └── contractiq.knowledge.index.replace
        └── Azure AI Search upload requests
```

The successful captured run indexed five fictional documents as 22 chunks in
12.11 seconds.

### Retrieval, model, and read tools

Trace `61e6e509e764ceec1b42e43753247477` contains the complete action-preparation
request:

```text
POST /api/v1/assistant/answers
└── contractiq.assistant.ask
    ├── PostgreSQL contract query
    ├── contractiq.knowledge.search
    │   ├── Foundry query embedding
    │   └── Azure AI Search hybrid retrieval
    └── contractiq.ai.model.generate
        ├── Foundry chat request
        ├── deterministic cancellation assessment tool
        └── cancellation preparation tool
```

The response used deployment `contractiq-chat`, returned eight application-owned
citations, and proposed `create_cancellation_request` with explicit confirmation
required. No write occurred during model invocation.

### Confirmed CQRS boundary

Trace `be497206ffbea6234130fdbb46fd224c` contains:

```text
POST /api/v1/assistant/actions/cancellation-requests
└── contractiq.assistant.tool.execute
    └── contractiq.cancellation.create
        └── PostgreSQL idempotency lookup
```

The local database already contained an open ACME request. The validation reused
its original idempotency key and returned HTTP 200 with outcome `replayed`; it did
not create or alter a row. This proves that the confirmed agent action still
crosses the application-owned CQRS handler and idempotency boundary. Automated
tests cover both the initial `created` outcome and the subsequent `replayed`
outcome.

## Telemetry privacy inspection

The Aspire span detail was inspected for the read tool and state-changing tool.
Exported attributes contained operation name, allow-listed tool name, outcome,
provider/model metadata, counts, and state-changing classification.

They did not contain:

- user question, system prompt, answer, or retrieved chunk text;
- document key, title, source path, or document content;
- customer, contract, cancellation-request, or chunk identifiers;
- idempotency key, access token, API key, connection string, or authorization
  header.

Automated activity-listener tests enforce the indexing and confirmed-command
correlation and assert that representative business content and identifiers are
absent from application-owned tags.

## Cost and cleanup record

The first indexing attempt exposed that the console host had not been started,
so logs arrived but activities were not exported. It still completed five
bounded embedding batches covering 22 short fictional chunks. After fixing the
host lifecycle, the temporary Search index was removed and one equivalent run
was made to capture the trace. Total indexing diagnostics therefore used ten
embedding requests and 44 chunk inputs, with zero automatic retries.

The agent evidence added one query embedding and one bounded chat request. The
confirmation replay was local and made no model or Search request. A mistaken
local endpoint path returned HTTP 404 before application execution and made no
Azure call.

Temporary index `contractiq-telemetry-v1` was deleted after capture. A direct
read returned `No index with the name ... was found`, confirming cleanup. The
primary `contractiq-knowledge-v1` index and the reviewed portfolio resources
remain available for the next evaluation slice.

Review the paid-capable resources by 2026-09-27 and delete the development
resource group before the assumed 2026-09-30 promotional-credit expiry when it
is no longer required.
