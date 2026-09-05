# Grounded contract assistant

The grounded assistant explains a contract question in English or Brazilian Portuguese by combining two application-owned inputs:

- a deterministic cancellation assessment calculated by the .NET domain model;
- citation-ready contract and policy evidence retrieved from the configured knowledge index.

The language model writes the explanation. It is not the authority for eligibility, dates, chargeable periods, penalty amounts, document scope, or state changes.

## Provider choice

The committed default is local Ollama so cloning or starting the repository never
creates a hosted-model charge. The assistant chat provider can be changed to Kimi
or Microsoft Foundry through local configuration. Embeddings can independently
remain on Ollama or use a Foundry embedding deployment.

Retrieval independently defaults to PostgreSQL full-text search plus pgvector.
The optional Azure AI Search adapter uses the same application-owned scope and
citation contract. See [Azure AI Search adapter](../azure/azure-ai-search-adapter.md).

No chat provider is called during application startup or automated tests. A hosted
request occurs only when a user submits a sufficiently grounded assistant question.

When Foundry is selected, the same fictional question, assessment, bounded
evidence, and tool schemas are sent to the configured Azure resource. Access is
keyless through `DefaultAzureCredential` and Entra RBAC. See
[Microsoft Foundry model adapters](../azure/foundry-model-adapters.md) for the
configuration and cost boundary.

When Kimi is selected, the question, deterministic assessment, bounded excerpts
from the fictional contract and policies, tool schemas, and requested read-tool
results leave the developer machine. Use only fictional sample data in this
portfolio demo. The configured Kimi endpoint must use HTTPS so the API key and
payload are never sent over plaintext HTTP.

## Local Ollama setup

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

## Kimi setup

Kimi uses its OpenAI-compatible Chat Completions endpoint and supports the same
function-calling flow used by the local adapter. You need a Kimi Open Platform API
key with API credits. Do not commit the key, place it in React, or paste it into an
issue or pull request.

The Kimi adapter omits the generic `Temperature` setting because K2.6 accepts only
provider-defined fixed values. It also explicitly disables K2.6 thinking so the
provider-specific `reasoning_content` field is not required across the assistant's
multi-step tool calls. These settings do not change deterministic domain decisions.

Store the provider selection and key in .NET user secrets on your own machine:

```powershell
dotnet user-secrets set "Assistant:Provider" "Kimi" --project src/ContractIQ.Api
dotnet user-secrets set "Assistant:Kimi:ApiKey" "PASTE-YOUR-KEY-LOCALLY" --project src/ContractIQ.Api
```

The non-secret defaults are:

```json
{
  "Assistant": {
    "Kimi": {
      "Endpoint": "https://api.moonshot.ai/v1",
      "ChatModel": "kimi-k2.6"
    }
  }
}
```

Alternatively, set the standard `MOONSHOT_API_KEY` environment variable and set
only the provider through user secrets. Start PostgreSQL and Ollama for local
embeddings, but the larger `qwen3:4b` chat model does not need to be loaded:

```powershell
docker compose --profile local-ai up -d postgres ollama
dotnet run --project src/ContractIQ.Api
```

To return to the zero-cost local chat provider:

```powershell
dotnet user-secrets set "Assistant:Provider" "Ollama" --project src/ContractIQ.Api
dotnet user-secrets remove "Assistant:Kimi:ApiKey" --project src/ContractIQ.Api
```

A Kimi Code subscription credential may use a different endpoint and may be
restricted to coding agents. For this application, use a key issued by the Kimi
Open Platform unless the account documentation explicitly states otherwise.

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
IChatClient -> Ollama, hosted Kimi, or Microsoft Foundry
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

The Application project depends on `IAssistantAnswerGenerator`. Infrastructure implements it behind Microsoft's `IChatClient` abstraction with OllamaSharp, an OpenAI-compatible Kimi client, or a keyless Microsoft Foundry client. Embeddings use the same provider-neutral approach. This keeps orchestration and tests independent from the provider without moving domain authority into model integration.

The same assistant can now prepare a cancellation action through safe tool calling. See [Safe assistant tool calling](safe-tool-calling.md) for the human-confirmation and write boundary.
