# ADR 0003: Use PostgreSQL for local hybrid retrieval

- Status: Accepted
- Date: 2026-08-28

## Context

The portfolio MVP needs credible retrieval-augmented generation with document versions, scope filters, citations, lexical relevance, and vector similarity. The default demonstration must remain free of Azure dependencies and reasonably easy for another developer to run.

Azure AI Search is a strong target for an optional hosted profile, but requiring it now would introduce an account, billable resources, additional infrastructure, and a second persistent data service before the retrieval boundaries have been proven.

## Decision

Use the existing local PostgreSQL database for the MVP knowledge index:

- generated `tsvector` columns and `ts_rank_cd` for lexical retrieval;
- pgvector cosine similarity with an HNSW index for semantic retrieval;
- Reciprocal Rank Fusion to combine lexical and vector rank positions;
- customer, contract, and effective-version filters before ranking;
- Ollama `embeddinggemma` through `Microsoft.Extensions.AI` for local multilingual embeddings;
- application-owned ports so hosted embedding and search adapters can be added later.

PostgreSQL lexical ranking is described accurately as lexical full-text search. It is not labeled BM25. Azure AI Search remains the planned adapter when demonstrating hosted BM25 and managed hybrid search adds enough value to justify the external resource.

## Consequences

The local profile has no cloud token or search-service cost and reuses the operational skills already required for the structured application database. Integration tests can validate the full query against a real pgvector container without requiring a model download by substituting deterministic embeddings.

The MVP does not demonstrate BM25, semantic ranker, managed search scaling, or Azure search operations. PostgreSQL and the embedding schema are coupled to the selected 768-dimension model in the current migration. Changing embedding dimensions requires a deliberate schema migration and reindex.
