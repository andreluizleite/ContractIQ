# Optional Azure AI foundation

This folder contains the reviewed infrastructure boundary for ContractIQ's optional Azure profile. Merely cloning, building, or testing the repository does not deploy it.

## Intended resources

| Resource | Configuration | Cost behavior |
| --- | --- | --- |
| Resource group | One isolated development group | No charge |
| Subscription budget | USD 10 planning target with 50%, 80%, and 100% alerts | No charge; alerts do not stop spend |
| Microsoft Foundry account and project | `AIServices`, public development endpoint, local authentication disabled | Account/project have no committed throughput |
| Optional model deployments | `gpt-5-mini` and `text-embedding-3-small`, `GlobalStandard`, 1K TPM each | No fixed deployment charge; inference is usage-based |
| Azure AI Search | Free tier, one shared service with a 50 MB limit | No charge while the free tier remains available and its limits are respected |
| Role assignments | Least-privilege roles for the local developer and optional GitHub OIDC service principal | No charge |

Model deployments are declared but disabled by default through `deployModels=false`.
The selected names, versions, SKU, capacity, and region were resolved against
the live catalog and subscription quota on 2026-08-31. They must be checked
again if the deployment is executed later or in another subscription.

The template outputs `foundryOpenAIEndpoint` in the form expected by the .NET
chat and embedding adapters: `https://<resource-name>.openai.azure.com/openai/v1/`.

## Security choices

- Local development uses the signed-in Microsoft Entra identity through `DefaultAzureCredential`.
- Local authentication is disabled on Foundry and Search, so application requests use inbound Entra RBAC and no API key is required or committed.
- The Free Search resource has no managed identity of its own. Managed identity for outbound Search connections requires Basic or higher.
- ContractIQ calls the Foundry embedding adapter itself and pushes the resulting vectors to Search. It does not use Search integrated vectorization, so the Free limitation does not break the portfolio flow.
- A production evolution can select Basic to add a Search-managed identity, integrated vectorization, semantic ranking, dedicated capacity, and stronger operational guarantees.
- Public endpoints keep this portfolio environment small. Private endpoints, a VNet, and Key Vault would add cost and operational complexity without improving the local-only demo enough to justify them.
- Only fictional sample contracts and policies may be indexed or sent to a hosted model.
- The optional `smokeTestPrincipalId` receives only Foundry inference plus Search schema/data roles on the individual resources. GitHub exchanges OIDC claims for a short-lived Azure token; no client secret is stored.

## Validate without deploying

Build the templates locally:

```powershell
az bicep build --file infra/azure/main.bicep
```

The pull-request workflow performs the same compile-time validation without signing in to Azure. A future predeployment review may run `what-if`, but only after region, quota, and parameters are approved.

## Required predeployment evidence

Before anyone runs a subscription deployment, record all of the following in the pull request:

1. The active subscription and tenant, without secrets.
2. The selected region and evidence that both Foundry models and Azure AI Search are available there.
3. The chat and embedding model names, versions, supported SKU, and unallocated quota.
4. Confirmation that the Search SKU is `free`, semantic ranking is disabled, and Search makes no managed-identity outbound connection.
5. The signed-in developer's Entra object ID for RBAC.
6. The budget email, budget start date, and USD 10 amount.
7. The exact `what-if` output.
8. Explicit approval to provision.

Set `deployModels=true` only in an explicitly reviewed deployment. After
provisioning is approved, follow the [manual keyless smoke-test guide](../../docs/azure/manual-smoke-test.md).
The live workflow is never triggered by a push or pull request and performs
only one bounded indexing/query scenario.

## Teardown boundary

All billable-capable resources are kept in the exact resource group `rg-contractiq-ai-dev`. After preserving any required screenshots and validation evidence, delete that resource group from the Azure portal or with an explicitly reviewed CLI command. The subscription-level budget can then be removed separately.

Deletion is intentionally not automated from a pull request or a normal CI run.
