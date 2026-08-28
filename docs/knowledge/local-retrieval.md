# Local knowledge retrieval

This delivery adds a complete retrieval slice without requiring Azure. It ingests fictional Markdown contracts and policies, produces multilingual embeddings through Ollama, stores chunks in PostgreSQL with pgvector, and returns scoped evidence with citation metadata.

It is retrieval, not the final conversational assistant. The later assistant will combine these citations with structured contract data and deterministic cancellation assessments.

## Flow

1. `sample-data/knowledge/manifest.json` supplies document identity, type, version, language, customer and contract scope, effective dates, and source path.
2. The Markdown chunker recognizes `##` sections and `<!-- page: N -->` markers so every chunk can become a precise citation.
3. `ContractIQ.DocumentIndexer` computes a SHA-256 document checksum. An unchanged version indexed by the same model is skipped.
4. Ollama generates 768-dimension `embeddinggemma` vectors through the standard `Microsoft.Extensions.AI` abstraction.
5. PostgreSQL stores document metadata, generated `tsvector` values, and pgvector embeddings. GIN and HNSW indexes support the two retrieval paths.
6. Search first selects documents visible to the requested customer and contract and effective on the requested date.
7. Lexical ranking and cosine vector ranking each produce candidates. Reciprocal Rank Fusion (RRF) combines their positions into the final ranking.

The application returns evidence only. It does not infer a penalty or change contract state. Those responsibilities remain in the domain model and CQRS command/query handlers.

## Local setup

From the repository root:

```powershell
docker compose --profile local-ai up -d postgres ollama
docker compose exec ollama ollama pull embeddinggemma
dotnet run --project tools/ContractIQ.DocumentIndexer
dotnet run --project src/ContractIQ.Api
```

The first two commands download a container image and an embedding model. There is no Azure consumption cost, but the files occupy local disk and embedding generation uses local compute.

The ordinary demo remains available with only PostgreSQL:

```powershell
docker compose up -d postgres
dotnet run --project src/ContractIQ.Api
```

In that mode, contract and cancellation endpoints work normally. Knowledge search returns `503 ollama_unavailable` until Ollama and the model are available.

## Search request

`POST /api/v1/knowledge/search`

```json
{
  "query": "Can ACME cancel without a penalty?",
  "customerId": "11111111-1111-4111-8111-111111111111",
  "contractId": "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa",
  "asOf": "2026-08-28",
  "limit": 5
}
```

`asOf` is optional and defaults to the server's current UTC date. `limit` defaults to 5 and accepts values from 1 through 20.

Each result includes the document key, title, type, version, language, source path, section, page, content, fused score, and available lexical/vector scores. Those fields are the citation contract for the future assistant and React experience.

## PostgreSQL lexical search is not BM25

The local implementation uses PostgreSQL full-text search with the language-neutral `simple` text-search configuration and `ts_rank_cd`. This is lexical relevance ranking, but it is not BM25.

That distinction is intentional and documented rather than hidden behind a misleading generic label. A future Azure profile may implement the same application retrieval port with Azure AI Search, whose text retrieval uses BM25. The MVP keeps PostgreSQL because it is free locally, already owns the structured demo data, and is sufficient to demonstrate hybrid retrieval and RRF without operating another service.

## Why RRF

Lexical and vector scores have different scales and should not be added directly. RRF combines rank positions instead. A chunk that ranks well in both candidate lists receives contributions from both, while a strong result from only one path can still appear.

The current constant is 60 and the candidate pool is at least 20 items or ten times the requested result limit. These values are pragmatic MVP defaults and can later be evaluated against a labeled retrieval dataset.

## Version and scope behavior

- A contract document is visible only when its customer and contract identifiers match the request.
- A global policy has null scope and is visible to every contract.
- Only versions effective on `asOf` are candidates.
- When multiple eligible versions share a document key, only the latest effective version is ranked.
- Reindexing the same version replaces its chunks atomically only when content or embedding model changes.

Automated integration tests verify scope isolation, effective version selection, hybrid ranking execution, citation metadata, migration behavior, and pgvector availability. They use deterministic test vectors, so CI does not download or depend on Ollama.

## Troubleshooting

Confirm that Ollama is healthy and the model exists:

```powershell
docker compose --profile local-ai ps
docker compose exec ollama ollama list
```

If the indexer reports that Ollama or `embeddinggemma` is unavailable, pull the model and run the indexer again. If search returns no results after resetting the PostgreSQL volume, rerun the indexer because sample document indexing is intentionally separate from application seed data.
