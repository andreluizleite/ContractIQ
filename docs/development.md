# Local development

This guide describes the shared development workflow for ContractIQ. The project is designed to run locally without an Azure subscription.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Node.js 24 LTS](https://nodejs.org/)
- npm, included with Node.js
- Git

Docker Desktop will be required when PostgreSQL and the local infrastructure are introduced. It is not required for the initial solution and frontend checks.

## Restore dependencies

From the repository root, restore the backend:

```powershell
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

```powershell
Set-Location src/ContractIQ.Web
npm run dev
```

The Vite development server prints its local URL after startup.

## Formatting

The root `.editorconfig` defines shared whitespace, line ending, C#, frontend, and documentation conventions. Format backend changes before opening a pull request:

```powershell
dotnet format ContractIQ.slnx
```

Frontend formatting and lint rules are enforced through the scripts in `src/ContractIQ.Web/package.json`.

Code should remain conventionally formatted and easy to scan. Comments should explain why a constraint or non-obvious decision exists rather than restating the code.

## Local configuration and secrets

Do not commit credentials, API keys, connection strings, access tokens, certificates, or populated `.env` files.

When a component introduces local settings, commit a safe example such as `.env.example` and keep actual values in ignored local files, environment variables, or .NET user secrets. The default development path must continue to work without Microsoft Foundry or other paid Azure resources.

## Troubleshooting

Confirm that the expected toolchains are active:

```powershell
dotnet --version
node --version
npm --version
```

If `npm ci` reports that the lock file and manifest differ, do not bypass the error. Run `npm install` only when the dependency change is intentional, review the updated lock file, and commit both files.

If CI reports formatting differences, run `dotnet format ContractIQ.slnx`, review the edits, and rerun the checks above.
