# Grounded contract assistant

The grounded assistant explains a contract question in English or Brazilian Portuguese by combining two application-owned inputs:

- a deterministic cancellation assessment calculated by the .NET domain model;
- citation-ready contract and policy evidence retrieved from the local knowledge index.

The language model writes the explanation. It is not the authority for eligibility, dates, chargeable periods, penalty amounts, document scope, or state changes.

## Local setup

The assistant requires both local models:

```powershell
docker compose --profile local-ai up -d postgres ollama
docker compose exec ollama ollama pull embeddinggemma
docker compose exec ollama ollama pull qwen3:4b
dotnet run --project tools/ContractIQ.DocumentIndexer
dotnet run --project src/ContractIQ.Api
```

`embeddinggemma` creates query and document embeddings. `qwen3:4b` generates the bilingual explanation. The model files stay in the local `contractiq-ollama-data` Docker volume. No Azure credential, hosted resource, or token charge is required.

The chat model is a larger optional download of approximately 2.5 GB. Normal customer, contract, assessment, and cancellation-request endpoints remain available without it.

## API request

`POST /api/v1/assistant/answers`

```json
{
  "question": "Can ACME cancel its contract now and what penalty applies?",
  "customerId": "11111111-1111-4111-8111-111111111111",
  "contractId": "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa",
  "language": "en"
}
```

Accepted language values are `en` and `pt-BR`. The same requests are executable from `src/ContractIQ.Api/ContractIQ.Api.http` and from the React contract workspace.

## Orchestration flow

```text
Question, customer, contract, language
                  |
                  v
Validate scope and load structured contract
                  |
                  v
Calculate deterministic cancellation assessment
                  |
                  v
Run scoped hybrid knowledge retrieval
                  |
          +-------+--------+
          |                |
  contract clause      no contract clause
          |                |
          v                v
build safe prompt     localized refusal
          |
          v
IChatClient -> Ollama qwen3:4b
          |
          v
answer + application-owned citations + assessment
```

Retrieval uses the assessment's current UTC request date, so the generated answer receives the contract version effective for the same date as the deterministic calculation.

## Citation contract

Every successful grounded response includes citations assembled from retrieval metadata:

```json
{
  "number": 1,
  "documentKey": "contract-acme-managed-services",
  "title": "ACME Managed Services Agreement",
  "version": "2.0",
  "section": "Termination for convenience",
  "page": 2,
  "sourcePath": "contracts/acme-managed-services-v2.md"
}
```

The prompt asks the model to use corresponding markers such as `[1]` inline. The React experience renders the authoritative source list separately, so citation metadata does not depend on the model inventing paths, versions, or pages.

## Insufficient evidence

Generation is skipped unless retrieval contains a contract document matching both the requested customer and contract. A global policy alone is not enough to answer a contract-specific question.

In that case the application returns a localized explanation with:

- `hasSufficientEvidence: false`;
- no citations;
- no model identifier because the chat model was not invoked;
- the deterministic assessment for transparency.

This behavior is deterministic and covered in both languages.

## Untrusted content boundary

User questions and retrieved document text are untrusted input. The application:

- enforces customer and contract scope before generation;
- limits question and evidence sizes;
- supplies document content as serialized data below a system instruction;
- explicitly instructs the model never to follow commands or role changes found in evidence;
- prevents the read-only assistant from claiming a request was created;
- returns citations from application metadata, not model-authored metadata;
- refuses when applicable contract evidence is absent.

These measures reduce prompt-injection risk but do not make model output authoritative. A future production profile should add model and prompt evaluations, content filtering where appropriate, and telemetry that avoids logging full contracts or prompts.

## Provider boundary

The Application project depends on `IAssistantAnswerGenerator`. Infrastructure implements it with OllamaSharp behind Microsoft's `IChatClient` abstraction. This keeps the orchestration and tests independent from the provider and allows a later Microsoft Foundry adapter without moving domain authority into the model integration.

The same assistant can now prepare a cancellation action through safe tool calling. See [Safe assistant tool calling](safe-tool-calling.md) for the human-confirmation and write boundary.
