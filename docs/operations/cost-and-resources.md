# Cost and resource management

ContractIQ v1 is designed to be evaluated without an Azure subscription and without a hosted-model charge. Every paid or resource-intensive capability is opt-in.

## Profiles and costs

| Profile               | Services                                  | Monetary cost                                                           | Local footprint                                                     |
| --------------------- | ----------------------------------------- | ----------------------------------------------------------------------- | ------------------------------------------------------------------- |
| Structured demo       | PostgreSQL/pgvector, API, React           | No external charge                                                      | Database image, volume, .NET and Node processes                     |
| Local retrieval       | Structured demo + Ollama `embeddinggemma` | No external charge                                                      | Approximately 622 MB for the embedding model plus indexing CPU      |
| Fully local assistant | Local retrieval + Ollama `qwen3:4b`       | No external charge                                                      | Approximately 2.5 GB more disk plus local RAM/CPU during generation |
| Kimi assistant        | Local retrieval + Kimi API                | Consumes the account's provider credits per submitted grounded question | The larger local chat model is not required                         |
| Local observability   | Any profile + Aspire Dashboard            | No external charge                                                      | Dashboard image, container memory, and temporary telemetry storage  |
| Optional Azure profile | Foundry + free Azure AI Search            | Search Free has no hourly charge; Foundry model inference consumes credit only after a model is deployed and invoked | Adapters and infrastructure definitions do not create resources by themselves |

Model sizes are approximate and can change when a model image is updated. Docker images, package caches, database records, and generated build artifacts require additional local disk space.

## Zero-cost default

The following flow does not contact Kimi, OpenAI, Microsoft Foundry, Azure AI Search, or another paid AI service:

```powershell
docker compose up -d postgres
dotnet run --project src/ContractIQ.Api
```

In another terminal:

```powershell
Set-Location src/ContractIQ.Web
npm run dev
```

Customer navigation, structured contract details, deterministic assessments, and confirmed cancellation requests remain available. Automated tests and deterministic AI evaluations also run without a hosted key.

## Local AI resource controls

Start Ollama only when embeddings or local generation are required:

```powershell
docker compose --profile local-ai up -d postgres ollama
```

List downloaded models:

```powershell
docker compose exec ollama ollama list
```

Remove only the optional local chat model while retaining embeddings:

```powershell
docker compose exec ollama ollama rm qwen3:4b
```

Stop Ollama without deleting models:

```powershell
docker compose stop ollama
```

The `contractiq-ollama-data` volume keeps downloaded models between container restarts. The `contractiq-postgres-data` volume keeps structured and indexed demo data.

## Hosted Kimi controls

Kimi is opt-in. No provider request occurs during startup, restore, build, tests, indexing, or deterministic evaluation. Credits are consumed only when a sufficiently grounded assistant question is submitted.

Remove the local provider key and return to Ollama:

```powershell
dotnet user-secrets set "Assistant:Provider" "Ollama" --project src/ContractIQ.Api
dotnet user-secrets remove "Assistant:Kimi:ApiKey" --project src/ContractIQ.Api
```

The API key remains in .NET user secrets or an environment variable and is never sent to React, stored in PostgreSQL, committed to Git, or written to application telemetry.

Provider prices and account-credit rules can change. Check the provider account before an extended live evaluation and use only the fictional sample documents with the hosted adapter.

## Aspire resource controls

Start the local dashboard only when inspecting telemetry:

```powershell
docker compose --profile observability up -d aspire-dashboard
```

Stop it without affecting PostgreSQL:

```powershell
docker compose stop aspire-dashboard
```

OpenTelemetry export is disabled by default, so the API does not continuously send telemetry to a dashboard that was not explicitly configured.

## Safe cleanup

Stop and remove ContractIQ containers and its Compose network while retaining database and model volumes:

```powershell
docker compose --profile local-ai --profile observability down
```

Remove the local database and downloaded model volumes only when a full reset is intended:

```powershell
docker compose --profile local-ai --profile observability down --volumes
```

The second command is destructive to ContractIQ's local container data. The next API startup recreates the schema and fictional structured records, but local models must be downloaded and documents indexed again. It does not remove source files or touch an external database.

Build outputs can be recreated and are excluded from Git. Use the standard IDE or .NET clean operation when local disk cleanup is required; do not delete repository or user-profile directories broadly.

## Azure boundary and teardown

ContractIQ v1 remains locally runnable without Azure credentials or automatic
provisioning. The optional development resources were provisioned on 2026-09-01
after an explicit review and removed on 2026-09-03 after the portfolio evidence
was recorded. Merely cloning, building, or testing the repository does not create
or invoke them. Therefore:

- cloning, building, testing, and demonstrating v1 creates no Azure resource;
- `rg-contractiq-ai-dev` existed only for the explicitly approved portfolio
  validation and was never a prerequisite;
- Microsoft Foundry and Azure AI Search adapters remain optional and are not
  local runtime dependencies;
- the validated free Search profile used keyless inbound application calls
  through Entra RBAC, while application-owned code generated and uploaded
  embeddings;
- Search-managed outbound identity, integrated vectorization, semantic ranking, and dedicated capacity remain a documented Basic-or-higher production evolution;
- issue #13 records resource assumptions, budgets, keyless authentication,
  infrastructure as code, and the verified teardown;
- the deleted USD 10 budget was an alerting target, not a hard spending cap;
- model deployments remain disabled by default for any future deployment. The
  deleted development environment used `contractiq-chat` and
  `contractiq-embeddings` during its bounded validation.

The optional Azure smoke workflow also remains inert until a person selects the
`azure-dev` environment in GitHub Actions. One run makes one Foundry embedding
request with two short fictional inputs, writes one Search chunk, and performs
one hybrid query. SDK retries are disabled, concurrent runs are serialized, and
the resulting count/duration report is retained for seven days. See the
[manual keyless smoke-test guide](../azure/manual-smoke-test.md) for setup,
execution, and revocation.

The dated [Azure live-validation record](../azure/live-validation-2026-09-02.md)
lists the exact bounded calls, trace evidence, temporary-index cleanup, and
remaining review dates. This boundary prevents a documentation example or
ordinary CI run from accidentally creating a chargeable cloud resource.

### Verified teardown — 2026-09-03

After the final live evidence was preserved, the owner explicitly approved the
destructive teardown. Azure then confirmed:

- `az group exists --name rg-contractiq-ai-dev` returned `false`;
- no resource remained with resource group `rg-contractiq-ai-dev`;
- no budget remained with name `budget-contractiq-ai-dev`.

This removed the Azure AI Search service, Microsoft Foundry account, Foundry
project, model deployments, and portfolio budget. The local application, GitHub
repository, Bicep definitions, and dated evidence were unaffected. A future live
Azure demonstration requires a new reviewed deployment.
