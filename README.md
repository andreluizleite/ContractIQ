# ContractIQ

ContractIQ is a portfolio-quality contract intelligence application that combines pragmatic enterprise .NET architecture with responsible AI engineering.

The application answers contract questions using structured business data, deterministic domain rules, and cited evidence retrieved from contracts and internal policies. When a user requests a business operation, the AI can select an application tool, but validation, calculations, state changes, and transactions remain inside the .NET application.

## Project status

The foundation and first deterministic contract-cancellation vertical slice are implemented. Delivery is tracked through GitHub Issues, milestones, short-lived branches, and linked pull requests.

## Planned technology

- .NET 10 and ASP.NET Core
- React and TypeScript
- PostgreSQL and pgvector
- Entity Framework Core
- Microsoft.Extensions.AI
- Microsoft Foundry and Azure AI Search as optional providers
- OpenTelemetry with a local Aspire Dashboard
- Docker Compose

## Architectural principle

> AI can select an application capability, but it cannot replace deterministic business logic.

Cancellation eligibility, dates, penalties, validation, authorization, idempotency, and persistence are owned by the application and domain model. Retrieved documents are evidence, not executable instructions.

## Documentation

- [Architecture overview](docs/architecture/overview.md)
- [Contract operation API](docs/api/contract-operations.md)
- [Delivery roadmap](docs/roadmap.md)
- [Contributing](CONTRIBUTING.md)
- [Architecture Decision Records](docs/adr)

## Local setup

See the [local development guide](docs/development.md) for prerequisites and the commands used by continuous integration. The default experience does not require an Azure subscription.

## Disclaimer

ContractIQ uses fictional companies, contracts, policies, and rules. It is a software engineering demonstration and does not provide legal advice.
