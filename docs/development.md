# Local development

This guide describes the shared development workflow for ContractIQ. The project is designed to run locally without an Azure subscription.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Node.js 24 LTS](https://nodejs.org/)
- npm, included with Node.js
- [Docker Desktop](https://www.docker.com/products/docker-desktop/), with Docker Compose
- Git

An Azure subscription is not required. PostgreSQL, pgvector, the backend, and the frontend all run locally.

Ollama is optional. It is needed only when indexing sample documents or exercising the knowledge-search endpoint; the deterministic contract operations continue to work without it.

## Start the local database

The Compose service uses PostgreSQL 18 with pgvector 0.8.6 and persists its data in the named volume `contractiq-postgres-data`. Its port is published only on `127.0.0.1`.

The repository contains safe fictional defaults. To customize the port or local credentials, create an ignored `.env` file from the committed example:

```powershell
Copy-Item .env.example .env
```

Start the database and wait until its health check reports `healthy`:

```powershell
docker compose up -d postgres
docker compose ps
```

The default connection string is:

```text
Host=localhost;Port=5432;Database=contractiq;Username=contractiq;Password=contractiq
```

The Development environment reads this default from `appsettings.Development.json`. Override it when necessary through the standard ASP.NET Core `ContractIQ` connection-string setting:

```powershell
$env:ConnectionStrings__ContractIQ = 'Host=localhost;Port=55432;Database=contractiq;Username=contractiq;Password=your-local-value'
```

If `.env` changes the database name, user, password, or port, update the connection string in the shell as well. A Compose `.env` file configures containers; it does not automatically export values to `dotnet run`.

These public defaults are intentionally limited to local development. Do not reuse them in a shared or hosted environment.

## Database migrations

The API applies committed migrations and seed data during startup. To update the database without starting the API, restore the repository-pinned EF Core tool and run:

```powershell
dotnet tool restore
dotnet ef database update --project src/ContractIQ.Infrastructure --context ContractIqDbContext
```

The initial migration creates the application schema and enables the `vector` extension. Verify the installed extension when diagnosing local setup:

```powershell
docker compose exec postgres sh -c 'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -c "SELECT extversion FROM pg_extension WHERE extname = ''vector'';"'
```

Create a migration only when intentionally changing the persistence model:

```powershell
dotnet ef migrations add <MigrationName> --project src/ContractIQ.Infrastructure --context ContractIqDbContext
```

Review generated migrations before committing them. Migrations and their model snapshot belong in the Infrastructure project; application startup applies committed migrations and seeds the fictional demo records idempotently.

Stop the database while retaining its data:

```powershell
docker compose stop postgres
```

Remove the container and network while retaining the named volume:

```powershell
docker compose down
```

To deliberately reset all local database data, remove the volume and then recreate the service:

```powershell
docker compose down --volumes
docker compose up -d postgres
```

`docker compose down --volumes` is destructive for the local ContractIQ database. It does not affect source files or any external database. The next API startup recreates the schema and fictional records.

## Health behavior

- `/health/live` reports whether the running process can serve requests and does not execute a database check.
- `/health/ready` and the compatibility route `/health` include the `postgresql` check.

Database initialization is intentionally fail-fast: if PostgreSQL is unavailable during startup, the API does not begin listening. After a successful startup, readiness reflects ongoing database availability while liveness remains independent of the database check.

## Restore dependencies

From the repository root, restore the backend:

```powershell
dotnet tool restore
dotnet restore ContractIQ.slnx
```

Install the frontend dependencies from its project directory:

```powershell
Set-Location src/ContractIQ.Web
npm ci
Set-Location ../..
```

Use `npm install` only when intentionally changing frontend dependencies. Commit the resulting `package.json` and `package-lock.json` changes together.

## Build and test

Run the same backend checks used by continuous integration:

```powershell
dotnet format ContractIQ.slnx --verify-no-changes --no-restore
dotnet build ContractIQ.slnx --configuration Release --no-restore
dotnet test ContractIQ.slnx --configuration Release --no-build
```

Run the frontend checks:

```powershell
Set-Location src/ContractIQ.Web
npm run lint
npm run test
npm run build
Set-Location ../..
```

During implementation, use the faster development commands as needed:

```powershell
dotnet build ContractIQ.slnx
dotnet test ContractIQ.slnx
```

Run the backend API locally:

```powershell
dotnet run --project src/ContractIQ.Api
```

The API listens on `http://localhost:5186` by default. Use the executable request collection at `src/ContractIQ.Api/ContractIQ.Api.http` or see the [contract operation API guide](api/contract-operations.md) for the seeded demonstration flow.

## Run local knowledge retrieval

The `local-ai` Compose profile keeps the model runtime optional. Start it together with PostgreSQL:

```powershell
docker compose --profile local-ai up -d postgres ollama
docker compose exec ollama ollama pull embeddinggemma
docker compose exec ollama ollama pull qwen3:4b
```

The pulls download the local embedding model and conversational model to the named volume `contractiq-ollama-data`. They do not create an Azure resource or token charge, but they use local disk, memory, and CPU. `embeddinggemma` is approximately 622 MB and `qwen3:4b` is approximately 2.5 GB.

The assistant can instead use Kimi for chat and tool calling while retaining local
`embeddinggemma` retrieval. This avoids loading `qwen3:4b` into memory. The hosted
provider is opt-in and requires a local secret, so repository builds and tests never
consume Kimi credits. See [Grounded contract assistant](assistant/grounded-answers.md#kimi-setup)
for configuration and credential boundaries.

Index the committed fictional documents:

```powershell
dotnet run --project tools/ContractIQ.DocumentIndexer
```

Run the same command again to verify idempotency. Unchanged document versions are skipped by content checksum. Then start the API and execute the knowledge-search and grounded-assistant requests in `src/ContractIQ.Api/ContractIQ.Api.http`.

See [Local knowledge retrieval](knowledge/local-retrieval.md) for ranking and indexing details, and [Grounded contract assistant](assistant/grounded-answers.md) for generation, citations, refusal behavior, and safety boundaries.

In a second terminal, start the React application:

```powershell
Set-Location src/ContractIQ.Web
npm run dev
```

The Vite development server prints its local URL, normally `http://localhost:5173`. During local development, Vite proxies `/api` requests to the .NET API at `http://localhost:5186`, so both processes must remain running. The browser never needs the PostgreSQL credentials; only the API connects to the database.

## Formatting

The root `.editorconfig` defines shared whitespace, line ending, C#, frontend, and documentation conventions. Format backend changes before opening a pull request:

```powershell
dotnet format ContractIQ.slnx
```

Frontend formatting and lint rules are enforced through the scripts in `src/ContractIQ.Web/package.json`.

Code should remain conventionally formatted and easy to scan. Comments should explain why a constraint or non-obvious decision exists rather than restating the code.

## Local configuration and secrets

Do not commit real credentials, API keys, hosted connection strings, access tokens, certificates, or populated `.env` files.

The root `.gitignore` excludes `.env` and local settings while explicitly allowing `.env.example`. Keep actual values in ignored local files, environment variables, or .NET user secrets. The default development path must continue to work without Microsoft Foundry or other paid Azure resources.

## Troubleshooting

Confirm that the expected toolchains are active:

```powershell
dotnet --version
node --version
npm --version
```

If `npm ci` reports that the lock file and manifest differ, do not bypass the error. Run `npm install` only when the dependency change is intentional, review the updated lock file, and commit both files.

If CI reports formatting differences, run `dotnet format ContractIQ.slnx`, review the edits, and rerun the checks above.

If PostgreSQL does not become healthy, inspect its status and logs:

```powershell
docker compose ps
docker compose logs postgres
```

If port `5432` is already in use, change `CONTRACTIQ_POSTGRES_PORT` in `.env` and use the same port in `ConnectionStrings__ContractIQ`.

If the API reports a missing table or relation, confirm that the database is healthy and restart the API so startup initialization can apply migrations.
