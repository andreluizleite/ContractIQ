# Contract operation API

The first ContractIQ vertical slice exposes structured contract operations through a small CQRS application layer. PostgreSQL persists customers, contracts, and cancellation requests; the domain and application layers remain independent of the database adapter.

## Run locally

Start PostgreSQL and run the API from the repository root:

```powershell
docker compose up -d postgres
dotnet run --project src/ContractIQ.Api
```

The API applies pending migrations and seeds the fictional records idempotently before it starts accepting requests. The default HTTP address is `http://localhost:5186`. Open `src/ContractIQ.Api/ContractIQ.Api.http` in Visual Studio and run the requests from top to bottom for a complete ACME cancellation scenario.

The seed operation is idempotent: restarting the API keeps cancellation requests and does not duplicate the fictional records. To restore a clean demo database, follow the volume-reset procedure in the [local development guide](../development.md).

## Available operations

| Method | Route | Purpose |
| --- | --- | --- |
| `GET` | `/api/v1/customers` | List the fictional customers. |
| `GET` | `/api/v1/contracts/{contractId}` | Read structured contract details. |
| `GET` | `/api/v1/contracts/{contractId}/cancellation-assessment` | Calculate eligibility, termination date, and penalty using domain rules. |
| `POST` | `/api/v1/contracts/{contractId}/cancellation-requests` | Recalculate the assessment and create a `PendingReview` request. |

The OpenAPI document is available at `/openapi/v1.json` in the Development environment. Errors use RFC Problem Details and include a stable application error code and trace identifier without exposing exception details.

## Demo records

| Company | Customer ID | Contract ID | Scenario |
| --- | --- | --- | --- |
| ACME Corporation | `11111111-1111-4111-8111-111111111111` | `aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa` | Active contract within its minimum commitment; a penalty applies. |
| Globex Corporation | `22222222-2222-4222-8222-222222222222` | `bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb` | Active contract after its minimum commitment; no penalty applies. |
| Initech | `33333333-3333-4333-8333-333333333333` | `cccccccc-cccc-4ccc-8ccc-cccccccccccc` | Cancelled contract; creation is rejected. |
| Initech | `33333333-3333-4333-8333-333333333333` | `dddddddd-dddd-4ddd-8ddd-dddddddddddd` | Expired contract; creation is rejected. |

## Idempotent command behavior

Creating a cancellation request requires an `Idempotency-Key` header. The key is an opaque, non-empty value of at most 128 characters.

- The first valid call creates a request and returns `201 Created`.
- Repeating the same key for the same contract returns the original request with `200 OK`.
- Reusing the key for another contract returns `409 Conflict`.
- A different key cannot create a second open request for the same contract and returns `409 Conflict`.

The client supplies only the contract identifier and idempotency key. Dates, status, eligibility, and penalty are recalculated by deterministic .NET domain logic immediately before the state change; none of those business values are accepted from the caller or an AI model.

## Architecture boundary

The endpoint calls a focused command or query handler. Application ports hide PostgreSQL and Entity Framework Core, while the domain owns cancellation calculations and invariants. Persistence can change without changing the domain rules or HTTP contract.

This is also the boundary that a future AI tool will call: the model may choose the cancellation capability, but the same command handler remains the authority for validation and state changes.
