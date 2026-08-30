# Security policy

## Supported version

Security fixes are applied to `main` and to the latest published `v1.x` release.

## Reporting a vulnerability

Use [GitHub private vulnerability reporting](https://github.com/andreluizleite/ContractIQ/security/advisories/new). Do not open a public issue containing an API key, credential, private contract, exploit details, or other sensitive data.

Include the affected component, reproduction steps, expected impact, and any suggested mitigation. This portfolio repository has no paid bug-bounty program or guaranteed response SLA, but good-faith reports will be reviewed and credited when appropriate.

## v1 security boundary

ContractIQ v1 is a local portfolio demonstration with fictional companies, contracts, policies, and credentials. The API intentionally allows anonymous access only in the ASP.NET Core `Development` environment and refuses to start in `Staging` or `Production`.

Do not expose the local API, PostgreSQL, Ollama, or Aspire Dashboard to the public internet. Authentication and authorization with Microsoft Entra ID are tracked in issue #12 and are required before any hosted deployment.

See [the v1 security review](docs/security/v1-security-review.md) for implemented controls, validation commands, data-flow boundaries, and residual risks.
