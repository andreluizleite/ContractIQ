# Local telemetry

ContractIQ uses vendor-neutral OpenTelemetry for traces, metrics, and structured
logs. The optional standalone .NET Aspire Dashboard receives the telemetry on the
developer machine. This profile does not create Azure resources and has no cloud
telemetry charge.

## Start the dashboard

From the repository root, start only the optional observability service:

```powershell
docker compose --profile observability up -d aspire-dashboard
docker compose --profile observability ps
```

Enable OTLP export for the current API terminal, then run the API:

```powershell
$env:OpenTelemetry__Enabled = 'true'
$env:OpenTelemetry__OtlpEndpoint = 'http://localhost:4317'
dotnet run --project src/ContractIQ.Api
```

Open `http://localhost:18888`. Anonymous access is enabled only for this local,
loopback-bound dashboard. The API remains fully functional when telemetry export
is disabled or the dashboard is not running.

Use `src/ContractIQ.Api/ContractIQ.Api.http` or the React interface to generate
traffic. Open a trace in the dashboard after asking the assistant a question. A
complete request can include these nested operations:

```text
HTTP request
└── contractiq.assistant.ask
    ├── PostgreSQL contract query
    ├── contractiq.knowledge.search
    │   ├── contractiq.knowledge.embedding.generate
    │   │   └── outbound model HTTP request
    │   └── contractiq.knowledge.index.query
    │       └── PostgreSQL hybrid search
    └── contractiq.ai.model.generate
        ├── outbound model HTTP request
        └── contractiq.assistant.tool
```

A confirmed cancellation adds `contractiq.assistant.tool.execute` and
`contractiq.cancellation.create` spans. PostgreSQL command spans are emitted by
Npgsql and remain children of the same request trace.

The document indexer can export its own short-lived trace to the same dashboard.
Set the telemetry variables in the terminal that runs the indexer:

```powershell
$env:OpenTelemetry__Enabled = 'true'
$env:OpenTelemetry__OtlpEndpoint = 'http://localhost:4317'
dotnet run --project tools/ContractIQ.DocumentIndexer
```

The indexer starts and stops its .NET host explicitly so the OpenTelemetry
providers are activated and flushed before the console process exits. A changed
document produces this correlated tree:

```text
contractiq.knowledge.index
└── contractiq.knowledge.document.index
    ├── contractiq.knowledge.index.check
    ├── contractiq.knowledge.embedding.generate
    │   └── provider embedding request
    └── contractiq.knowledge.index.replace
        └── PostgreSQL or Azure AI Search dependency request
```

An unchanged document ends after the check span with outcome `skipped`; it does
not generate embeddings or replace index data.

## Correlation

Every HTTP response includes `X-Correlation-ID`. The value is the W3C trace ID
used by OpenTelemetry and is also returned as `traceId` in API problem details.
Exported structured logs include the same value through a logging scope, so a
dashboard search can move from an error response to its logs and complete trace.
Metrics use trace-based exemplars, allowing a backend that supports exemplars to
link a representative measurement back to its sampled request trace.

Incoming distributed traces should use the standard W3C `traceparent` header.
ContractIQ does not accept an arbitrary caller-provided correlation value as a
trusted trace identity.

## Application metrics

The dashboard receives runtime, ASP.NET Core, HTTP client, and Npgsql metrics in
addition to these application-owned measurements:

- `contractiq.assistant.requests` and `contractiq.assistant.request.duration`;
- `contractiq.assistant.evidence.count` and `contractiq.assistant.citation.count`;
- `contractiq.knowledge.searches`, duration, and result count;
- `contractiq.knowledge.indexing.runs`, duration, indexed document count, and
  indexed chunk count;
- `contractiq.ai.model.requests`, duration, and provider-reported token counts;
- `contractiq.assistant.tool.calls` by tool, outcome, and state-changing class;
- `contractiq.cancellation.commands` and command duration.

Token metrics are emitted only when the provider reports usage. A missing value
does not mean zero tokens were consumed.

## Data protection boundary

Telemetry intentionally excludes:

- questions, prompts, answers, document chunks, and retrieved document content;
- API keys, authorization headers, connection strings, and idempotency keys;
- customer IDs, contract IDs, cancellation request IDs, and document paths;
- token content or model reasoning content.

Standard ASP.NET Core server spans keep the matched route template, such as
`/api/v1/contracts/{contractId:guid}`, but a privacy processor removes raw URL
path/full-URL attributes before export so route values cannot carry contract IDs.

Safe dimensions include operation name, provider and model name, language,
document type, outcome, duration, counts, and whether a tool is state-changing.
Document titles, keys, paths, checksums, and business identifiers are not span
attributes. The model HTTP instrumentation uses standard request metadata and
does not capture request or response bodies. EF Core parameter value logging is
not enabled.

Tool audit events retain business identifiers inside the application boundary so
a future authorized audit store can enforce access controls. The current exported
log deliberately omits those identifiers.

## Health endpoints

- `/health/live` checks only whether the process is serving requests.
- `/health/ready` and `/health` include PostgreSQL availability.

Health responses also include `X-Correlation-ID`. Automated integration tests
verify the liveness/readiness distinction and correlation between the response
header and problem details.

## Stop and troubleshoot

Stop the dashboard without affecting PostgreSQL:

```powershell
docker compose --profile observability stop aspire-dashboard
```

If no telemetry appears, confirm that the dashboard is running, that port `4317`
is available, and that the API was started after setting
`OpenTelemetry__Enabled=true`. Export is batched, so allow a few seconds after
generating a request.

To change ports in `.env`, keep `CONTRACTIQ_OTLP_GRPC_PORT` and the API's
`OpenTelemetry__OtlpEndpoint` value aligned.

The OpenTelemetry instrumentation is intentionally backend-neutral. A future
Azure deployment can export the same signals to Azure Monitor/Application
Insights without moving observability concerns into the domain or application
use cases.
