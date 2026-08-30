# Bilingual interview demonstration guide

This guide turns the running application into a short, repeatable interview narrative. The core flow takes about five minutes; the optional architecture discussion can extend it to ten or fifteen minutes.

All companies, documents, contracts, credentials, and values are fictional.

## Before the interview

From the repository root, confirm the deterministic experience:

```powershell
docker compose up -d postgres
dotnet run --project src/ContractIQ.Api
```

In another terminal:

```powershell
Set-Location src/ContractIQ.Web
npm ci
npm run dev
```

Open `http://localhost:5173` and verify that ACME, Globex, and Initech load.

For the RAG and assistant steps, prepare the local index before the interview:

```powershell
docker compose --profile local-ai up -d postgres ollama
docker compose exec ollama ollama pull embeddinggemma
dotnet run --project tools/ContractIQ.DocumentIndexer
```

Use either the local `qwen3:4b` chat model or the already configured optional Kimi provider. A hosted model is never called by startup or automated tests; it is called only when a sufficiently grounded question is submitted.

If an earlier rehearsal created a cancellation request, reset the fictional local database before the live demo:

```powershell
docker compose --profile local-ai --profile observability down --volumes
docker compose up -d postgres
```

This removes the local PostgreSQL and Ollama volumes. It is destructive only to ContractIQ's container data and downloaded local models; it does not delete source files or affect an external database.

## English core flow

### 1. Frame the problem

Say:

> ContractIQ helps a contract-operations user combine structured customer data, contract clauses, internal policies, and deterministic business rules. The AI explains and selects capabilities, but it is not the authority for money or state changes.

Point out the three-area workspace: customer navigation, deterministic contract decision, and grounded assistant.

### 2. Show the penalty scenario

Select **ACME Corporation** and **Contract AAAAAAAA**.

Expected result:

- cancellation is available;
- the earliest termination date includes the notice period;
- a positive penalty is shown because the contract remains inside its minimum commitment;
- the calculation displays chargeable periods × monthly fee × penalty rate.

Say:

> This card does not come from the LLM or RAG. It comes from the .NET domain model and structured PostgreSQL data, so the same input always produces the same result.

Do not memorize the displayed date or amount. They are calculated against the current UTC business date and will change as the fictional contract approaches its commitment end.

### 3. Show grounded RAG

Ask:

```text
Can ACME cancel this contract now, and what penalty would apply?
```

Expected result:

- the answer agrees with the deterministic assessment;
- citations identify the applicable contract and policy sections;
- the application, not the model, owns citation numbering and metadata.

Say:

> Retrieval is scoped by customer, contract, document type, and effective date before ranking. The model receives bounded evidence and cannot cite a document that the application did not retrieve.

If no applicable contract clause is indexed, show the localized insufficient-evidence response. That refusal is a successful safety behavior, not an application failure.

### 4. Show agent tool preparation

Ask:

```text
Prepare the cancellation request.
```

Expected result:

- the model may call read, assessment, search, and preparation tools;
- an action preview is returned;
- no cancellation-request row is created yet.

Say:

> The model can prepare an application capability, but the write tool is deliberately absent from automatic invocation.

Open **Review and confirm action**. The dialog shows the customer, contract, date, and deterministic penalty. Check the review checkbox only when you are ready to demonstrate persistence, then confirm.

Say:

> The write endpoint accepts no penalty, status, or eligibility from the model. The CQRS command reloads the contract, recalculates the domain assessment, validates the operation, and persists it in one transaction.

### 5. Contrast the no-penalty scenario

Select **Globex Corporation** and its active contract.

Expected result:

- cancellation remains available;
- the earliest termination date is after the minimum commitment;
- the deterministic penalty is zero.

Say:

> The UI and assistant are shared, but the domain result changes because the structured terms differ. This is why business rules do not belong in a prompt.

### 6. Demonstrate duplicate protection

After creating one ACME cancellation request, try to create another request for the same contract.

Expected result:

- replaying the same idempotency key returns the original request;
- using a new key while an open request exists returns a conflict;
- no duplicate open request is persisted.

Say:

> Idempotency, the open-request invariant, and the transaction are application and database concerns. They still work if the model provider is unavailable or replaced.

## Fluxo principal em português

### 1. Apresente o problema

Diga:

> O ContractIQ ajuda o time de operações contratuais a combinar dados estruturados, cláusulas, políticas internas e regras determinísticas. A IA explica e seleciona capacidades, mas não decide valores nem altera o estado diretamente.

Mostre as três áreas: navegação de clientes, decisão contratual e assistente fundamentado.

### 2. Mostre o cenário com multa

Selecione **ACME Corporation** e **Contrato AAAAAAAA**.

Resultado esperado:

- cancelamento disponível;
- primeiro término considerando o aviso prévio;
- multa positiva durante a fidelidade mínima;
- fórmula com períodos cobrados × mensalidade × taxa.

Diga:

> Este resultado não vem do LLM nem do RAG. Ele é calculado pelo domínio .NET com os dados estruturados do PostgreSQL.

### 3. Mostre o RAG com fontes

Pergunte:

```text
A ACME pode cancelar este contrato agora e qual multa seria aplicada?
```

Resultado esperado:

- a explicação concorda com a avaliação determinística;
- as fontes mostram as cláusulas de contrato e política aplicáveis;
- a aplicação controla a numeração e os metadados das citações.

Diga:

> A busca filtra cliente, contrato, tipo de documento e vigência antes de combinar relevância textual e vetorial. Sem evidência contratual suficiente, o assistente recusa a resposta.

### 4. Mostre a preparação da operação

Pergunte:

```text
Prepare a solicitação de cancelamento.
```

Abra **Revisar e confirmar ação**.

Diga:

> O agente apenas prepara a proposta. A gravação exige confirmação explícita, uma chave de idempotência e a execução do mesmo comando CQRS usado fora da IA.

Revise os dados, marque a confirmação e envie somente se quiser demonstrar a persistência.

### 5. Mostre o cenário sem multa

Selecione **Globex Corporation**.

Diga:

> A experiência é a mesma, porém o resultado é diferente porque o término ocorre depois da fidelidade mínima. A multa determinística é zero.

### 6. Mostre a duplicidade

Depois de criar a primeira solicitação da ACME, tente criar outra para o mesmo contrato.

Diga:

> A aplicação protege contra repetição de rede e contra uma segunda solicitação aberta. Essa garantia está no código e no banco, não no comportamento probabilístico do modelo.

## No-model fallback

The interview remains demonstrable if Ollama or Kimi is unavailable:

1. show customer and contract navigation;
2. compare ACME and Globex deterministic assessments;
3. open the direct cancellation confirmation dialog;
4. explain that the frontend keeps deterministic operations available while reporting the assistant dependency as unavailable;
5. use the versioned deterministic AI evaluation report as evidence of grounding and tool-routing gates.

This fallback is intentional. A model-provider outage must not remove the application's contract operations.

## Common interview questions

### Why a modular monolith?

The domain, application, infrastructure, API, and web boundaries are explicit, but the current scale does not justify distributed transactions, service discovery, or operationally independent services. The design can extract adapters or workloads later without paying that complexity in the MVP.

### Why CQRS without a mediator library?

Commands and queries have separate request/handler types and responsibilities. Endpoints inject focused handlers directly because an additional dispatch abstraction would not add meaningful behavior at this size.

### Why RAG instead of fine-tuning?

Contract and policy content changes, requires effective-date filtering, and must be cited. Retrieval makes the evidence inspectable and replaceable without retraining a model. Fine-tuning would not replace deterministic business rules or current document retrieval.

### Why one agent instead of multiple agents?

The use case needs one bounded assistant with a small tool set. Multiple autonomous agents would add coordination, latency, cost, and failure modes without improving the demonstrated business outcome.

### What would change in production?

Add Entra ID authentication and authorization, tenant isolation, managed secrets, a reviewed deployment topology, distributed abuse protection, production database roles, network controls, backup/restore, and provider cost governance. The v1 API deliberately refuses to start outside `Development` until those controls exist.

### How do you know the AI is safe enough?

Safety is layered: deterministic domain authority, scoped tools, application-owned citations, insufficient-evidence refusal, explicit confirmation, idempotent writes, privacy-aware telemetry, and deterministic evaluation scenarios in required CI. These controls constrain the model rather than assuming it is always correct.
