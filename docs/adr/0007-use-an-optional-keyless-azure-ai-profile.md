# ADR 0007: Use an optional keyless Azure AI profile

## Status

Accepted for incremental implementation. Provisioning still requires an explicit cost and `what-if` approval.

## Context

The local MVP proves retrieval, grounding, tool calling, and deterministic domain control with PostgreSQL, pgvector, Ollama, and optional Kimi chat. The portfolio also needs credible Microsoft cloud integration without making a paid account mandatory or moving business authority into an agent platform.

## Decision

Add an optional Azure profile behind the existing application ports:

- Microsoft Foundry supplies chat and embedding model inference;
- Azure AI Search supplies managed BM25 and vector hybrid retrieval;
- PostgreSQL continues to own structured business data and transactions;
- the .NET assistant remains the single orchestrator and exposes the existing bounded tools;
- local development authenticates through Microsoft Entra and `DefaultAzureCredential` rather than stored service keys;
- ordinary CI validates code and Bicep offline; live Azure smoke tests require manual dispatch;
- infrastructure is reproducible with Bicep, isolated in one development resource group, and guarded by a small subscription budget.

The first portfolio environment uses public service endpoints and RBAC. Private networking and hosted agents are deliberately excluded.

## Consequences

The project demonstrates provider substitution, managed hybrid search, keyless cloud access, infrastructure as code, cost controls, and deterministic AI safety boundaries. The local profile remains cloneable and demonstrable without Azure.

Public endpoints are less isolated than a production private network, and a budget alert is not a hard spending cap. Model availability, versions, quotas, prices, and regions must therefore be checked immediately before deployment. The Azure adapters also require explicit integration and live smoke tests while normal tests stay deterministic.
