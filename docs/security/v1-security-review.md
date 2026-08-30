# v1 security review

## Release decision

ContractIQ v1 is suitable for a local portfolio demonstration using fictional data. It is not approved for public hosting or real customer contracts.

The API has no user identity in v1. To prevent the demo profile from being mistaken for a production configuration, startup fails outside the ASP.NET Core `Development` environment. Microsoft Entra ID, scopes, authorization policies, and tenant isolation remain explicitly deferred to issue #12.

## Threat boundaries

The review considers:

- untrusted HTTP input, prompts, and indexed documents;
- accidental public exposure of anonymous read, AI, and write endpoints;
- hosted-model cost and contract-data egress;
- secrets in source, logs, telemetry, or CI;
- dependency and CI supply-chain compromise;
- repeated requests that exhaust local or hosted resources.

The repository contains only fictional data and loopback demo credentials. Docker Compose binds PostgreSQL, Ollama, and the optional Aspire Dashboard to `127.0.0.1`.

## Implemented controls

- non-Development API startup is rejected while authentication is absent;
- API request bodies are limited to 64 KiB by Kestrel;
- assistant, knowledge-search, and write endpoints use per-client fixed-window limits;
- rate-limit responses use sanitized `429` Problem Details;
- knowledge queries and assistant questions are limited to 1,000 characters;
- Kimi configuration requires HTTPS before its API key can be used;
- SQL is parameterized through EF Core/Npgsql;
- unexpected errors return generic Problem Details without stack traces;
- API responses include no-sniff, frame, referrer, content-security, and no-store headers;
- OpenAPI is exposed only in Development and permissive CORS is not enabled;
- raw server URL tags are removed from exported traces while route templates remain;
- prompts, answers, document bodies, credentials, and idempotency keys are not logged by application instrumentation;
- GitHub Actions have read-only permissions, immutable action SHAs, and no persisted checkout credential;
- dependency review, NuGet audit, npm audit, lock files, and weekly Dependabot updates cover the supply chain;
- local container images are pinned to reviewed manifest digests.

## Hosted Kimi data flow

Kimi is opt-in and may consume API credits. For every sufficiently grounded question, ContractIQ sends the hosted provider:

- the user's question;
- the deterministic cancellation assessment;
- bounded excerpts from the selected fictional contract and applicable policies;
- read-tool schemas and any tool results requested by the model.

The API key is read from .NET user secrets or `MOONSHOT_API_KEY` and is never sent to React. Only fictional sample data should be used with the portfolio demo. Returning to Ollama keeps chat generation local.

## Continuous verification

Run from the repository root:

```powershell
dotnet restore ContractIQ.slnx --locked-mode `
  -p:NuGetAudit=true `
  -p:NuGetAuditMode=all `
  -p:NuGetAuditLevel=moderate `
  -p:TreatWarningsAsErrors=true
dotnet list ContractIQ.slnx package --vulnerable --include-transitive --no-restore
Set-Location src/ContractIQ.Web
npm ci
npm audit --audit-level=high
```

Required CI also runs formatting, builds, automated tests, and deterministic AI evaluations without hosted credentials or paid services.

## Residual risks and deferred work

- In-process rate limits are demo safeguards, not distributed abuse protection.
- Health responses and citation metadata reveal harmless fictional implementation details.
- Database migrations and demo seed data run automatically because non-Development startup is blocked.
- TLS termination, forwarded-header trust, production health-network policy, WAF/API gateway, runtime database roles, image signing, and deployment SBOMs require an actual hosting topology.
- Authentication, authorization, RBAC, and tenant isolation belong to issue #12.
- Microsoft Foundry, Azure AI Search, managed identity, Key Vault, and Azure cost controls belong to issue #13.

Any future hosted profile must resolve these items and replace the local-demo startup guard with an authenticated, reviewed configuration.
