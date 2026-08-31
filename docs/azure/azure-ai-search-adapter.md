# Azure AI Search adapter

ContractIQ can use Azure AI Search as an optional knowledge index while
PostgreSQL remains the structured system of record. The committed default is
still PostgreSQL full-text search plus pgvector, so local startup, tests, and
the interview demonstration do not contact Azure.

## Responsibility boundary

The existing application-owned indexing and retrieval flow is unchanged:

1. `IKnowledgeDocumentCatalog` reads the fictional Markdown documents.
2. `MarkdownKnowledgeChunker` produces stable, citation-ready chunks.
3. `IKnowledgeEmbeddingGenerator` uses either Ollama or Microsoft Foundry.
4. `IKnowledgeIndex` selects PostgreSQL or Azure AI Search through configuration.
5. `SearchKnowledgeHandler` validates customer, contract, date, query, and limit
   before calling the selected adapter.

Azure AI Search does not calculate cancellation eligibility or penalties and
does not write contract operations to PostgreSQL. It stores only the optional
RAG index used to retrieve evidence.

## Versioned schema

The default index name is `contractiq-knowledge-v1`, and every chunk also stores
schema version `1`. The schema contains:

- a deterministic chunk key;
- document key, type, version, language, and content checksum;
- optional customer and contract scope;
- effective-from and effective-to dates;
- title, source path, section, page, and chunk text for citations;
- embedding model identity and a 768-dimension vector.

Changing an immutable field or vector dimension requires a new index name such
as `contractiq-knowledge-v2`. This avoids mutating an incompatible live schema.

## Idempotent indexing

Before generating embeddings, the application asks whether the same document
key, version, checksum, embedding model, and schema version already exist. An
unchanged document is skipped.

When replacement is required, chunk keys are derived deterministically from the
schema version, document key, document version, and chunk position. Existing
keys are updated with `mergeOrUpload`, while stale keys from a shortened document
are deleted. Running the same indexer again therefore does not create duplicate
chunks.

## One scoped hybrid query

Each retrieval executes one Azure AI Search request containing both:

- the user's validated text for BM25 keyword ranking;
- the application-generated query vector for HNSW similarity ranking.

Azure AI Search combines the ranking lists. The vector configuration explicitly
uses `PreFilter`, and the same top-level OData filter limits candidates by schema
version, effective date, customer, and contract before vector ranking. The
adapter maps the fused score and stored metadata back to the application-owned
`KnowledgeEvidence` record. Individual lexical and vector subscores are not
returned by this profile, so those optional fields remain `null`.

## Keyless authentication

The adapter uses the shared `TokenCredential`. For this local portfolio profile,
`DefaultAzureCredential` reads the signed-in Azure CLI identity. No Search admin
key is stored or committed.

The developer identity needs both roles already declared by the reviewed Bicep:

- `Search Service Contributor` to create or update the index schema;
- `Search Index Data Contributor` to query and update index documents.

A future Azure-hosted API should replace the local credential chain with a
specific managed identity while keeping the same application ports.

## Configuration after explicit provisioning

Do not configure this profile until the Azure resource has been separately
reviewed and provisioned. The API can use .NET user secrets:

```powershell
dotnet user-secrets set "Knowledge:IndexProvider" "AzureAiSearch" --project src/ContractIQ.Api
dotnet user-secrets set "AzureSearch:Endpoint" "https://<search-name>.search.windows.net" --project src/ContractIQ.Api
dotnet user-secrets set "AzureSearch:IndexName" "contractiq-knowledge-v1" --project src/ContractIQ.Api
```

Normal runtime defaults to three transient Search SDK retries. The bounded
manual smoke workflow sets `AzureSearch:MaximumRetries` to `0`; accepted values
are zero through five.

The document indexer is a separate process. Configure its current PowerShell
session without saving a key:

```powershell
$env:Knowledge__IndexProvider = "AzureAiSearch"
$env:AzureSearch__Endpoint = "https://<search-name>.search.windows.net"
$env:AzureSearch__IndexName = "contractiq-knowledge-v1"
dotnet run --project tools/ContractIQ.DocumentIndexer
```

Embedding configuration remains independent. Local Ollama embeddings can be
pushed to Search, or the Foundry embedding profile can be selected using the
settings in [Microsoft Foundry model adapters](foundry-model-adapters.md).

Remove the temporary environment variables after indexing:

```powershell
Remove-Item Env:Knowledge__IndexProvider
Remove-Item Env:AzureSearch__Endpoint
Remove-Item Env:AzureSearch__IndexName
```

Return the API to local retrieval with:

```powershell
dotnet user-secrets set "Knowledge:IndexProvider" "PostgreSql" --project src/ContractIQ.Api
```

## Failure, telemetry, and testing

Authentication, HTTP, timeout, throttling, and service failures cross the
existing safe `ExternalDependencyUnavailableException` boundary. Azure SDK
details are not exposed to the client.

Dependency telemetry records provider, operation, duration, item count, and
outcome. It excludes queries, document bodies, customer and contract identifiers,
endpoints, credentials, and vectors.

Automated tests inspect the versioned schema, verify one prefiltered hybrid
request, simulate idempotent replacement and citation mapping, and exercise safe
failure translation. They do not request an Azure token or contact Azure.

## Cost boundary

The adapter and its tests create no resource. A free Azure AI Search service has
no hourly charge but has strict capacity limits and must still be provisioned
explicitly. Foundry embeddings and chat can consume promotional credit only
after their model deployments are separately approved and invoked.
