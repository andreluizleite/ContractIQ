# ADR 0006: Use OpenTelemetry with an optional local dashboard

- Status: Accepted
- Date: 2026-08-28

## Context

The portfolio demo needs correlated visibility across HTTP, PostgreSQL,
retrieval, model calls, tools, and commands. The default development experience
must remain local and free, while leaving a credible path to Azure Monitor and
Application Insights later.

Telemetry can expose sensitive contract information if prompts, document chunks,
identifiers, request bodies, or secrets become attributes or logs. Observability
therefore needs an explicit data boundary rather than broad payload capture.

## Decision

Use OpenTelemetry as the vendor-neutral instrumentation standard and the
standalone .NET Aspire Dashboard as an opt-in Docker Compose profile.

The API composition root configures OTLP exporters. Application use cases expose
business operation spans and metrics through the native .NET `ActivitySource` and
`Meter` APIs; the domain remains telemetry-independent. ASP.NET Core, `HttpClient`,
runtime, and Npgsql use established instrumentation packages.

Export is disabled by default. The local dashboard binds only to loopback, is
started explicitly, and creates no Azure resources.

Prompts, answers, document content, secrets, business identifiers, idempotency
keys, and request/response bodies are not exported. Numeric token usage is
recorded only when the provider supplies it. Server spans retain route templates,
while a processor removes raw URL tags that could contain business identifiers.

## Consequences

- One trace connects transport, application orchestration, external providers,
  tools, commands, and persistence.
- Local demonstrations require only an additional container image and no cloud
  subscription.
- The same telemetry model can later target Azure Monitor or another OTLP
  backend without changing deterministic business logic.
- Export remains opt-in, so developers must enable it when they want to inspect
  telemetry.
- The local dashboard is a diagnostic experience, not a production retention,
  alerting, or access-control solution.
