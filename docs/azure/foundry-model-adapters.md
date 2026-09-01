# Microsoft Foundry model adapters

ContractIQ can use Microsoft Foundry for chat and embeddings without changing
the application or domain layers. The committed defaults remain Ollama for both
capabilities, so restore, startup, tests, and the local demonstration do not
contact Azure.

## Provider boundary

The Foundry adapters implement the existing application-owned ports through
`Microsoft.Extensions.AI`:

- `IAssistantAnswerGenerator` continues to expose the same four scoped read and
  preparation tools through `FunctionInvokingChatClient`;
- `IKnowledgeEmbeddingGenerator` returns the same 768-dimension vectors expected
  by the committed pgvector schema;
- domain rules, CQRS handlers, confirmations, transactions, citations, and
  PostgreSQL persistence are unchanged.

The Foundry project endpoint is not used for embeddings. Both chat completions
and embeddings use the resource's OpenAI-compatible endpoint:

```text
https://<resource-name>.openai.azure.com/openai/v1/
```

## Keyless authentication

The adapters create an OpenAI-compatible client with `DefaultAzureCredential`.
On a developer machine it can use the current Azure CLI login. A future hosted
API can use a managed identity without changing application code.

No Foundry API key is read, stored, logged, sent to React, or committed. The
caller must have the `Cognitive Services OpenAI User` role on the Foundry
resource.

## Configuration

Select chat and embedding providers independently:

```powershell
dotnet user-secrets set "Assistant:Provider" "Foundry" --project src/ContractIQ.Api
dotnet user-secrets set "Knowledge:EmbeddingProvider" "Foundry" --project src/ContractIQ.Api
dotnet user-secrets set "Foundry:OpenAIEndpoint" "https://<resource-name>.openai.azure.com/openai/v1/" --project src/ContractIQ.Api
dotnet user-secrets set "Foundry:ChatDeployment" "<chat-deployment-name>" --project src/ContractIQ.Api
dotnet user-secrets set "Foundry:EmbeddingDeployment" "<embedding-deployment-name>" --project src/ContractIQ.Api
dotnet user-secrets set "Foundry:EmbeddingDimensions" "768" --project src/ContractIQ.Api
```

The endpoint must use HTTPS and end with `/openai/v1/`. Deployment names cannot
be empty. Embedding dimensions must be explicitly configured and must equal 768
while the local pgvector schema remains `vector(768)`. Normal runtime defaults
to three transient retries; the bounded manual smoke test overrides
`Foundry:MaximumRetries` to `0` so a failed run cannot repeat model consumption
automatically.

The provider-neutral chat options normally map the output limit to the legacy
`max_tokens` field. GPT-5 deployments reject that field, so the Foundry adapter
omits it and sends the same configured limit as `max_completion_tokens` through
the official OpenAI client options. Ollama and Kimi keep their existing option
mapping.

Changing the embedding deployment changes the stored embedding model identity.
Run the document indexer again so document chunks are regenerated consistently.

## Failure and telemetry behavior

Authentication, HTTP, provider, rate-limit, and timeout failures are translated
to the existing safe `ExternalDependencyUnavailableException` boundary. The API
therefore returns the established dependency-unavailable response without
exposing Azure SDK details.

Chat and embedding dependency spans and metrics record only operation, provider,
deployment, duration, token counts when supplied, and outcome. Prompts, document
content, answers, credentials, and retrieved evidence are not telemetry tags.

## Cost and test boundary

Selecting `Foundry` does not itself deploy a model. Requests work only after the
separate infrastructure, region, deployment, SKU, and quota steps are explicitly
approved and completed. Model calls can consume Azure credit.

Automated tests replace chat and embeddings with deterministic implementations.
They validate configuration, provider selection, orchestration, safety, and
shape constraints without acquiring an Azure token or making a live model call.
