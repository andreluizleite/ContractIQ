# Optional Azure AI foundation

This folder contains the reviewed infrastructure boundary for ContractIQ's optional Azure profile. Merely cloning, building, or testing the repository does not deploy it.

## Intended resources

| Resource | Configuration | Cost behavior |
| --- | --- | --- |
| Resource group | One isolated development group | No charge |
| Subscription budget | USD 10 planning target with 50%, 80%, and 100% alerts | No charge; alerts do not stop spend |
| Microsoft Foundry account and project | `AIServices`, public development endpoint, local authentication disabled | Account/project have no committed throughput; model inference is usage-based |
| Azure AI Search | Free tier, one shared service with a 50 MB limit | No charge while the free tier remains available and its limits are respected |
| Role assignments | Least-privilege roles for the local developer | No charge |

No model deployment is declared yet. Model name, version, SKU, capacity, and region must be resolved against the live catalog and subscription quota immediately before a separate approved deployment.

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

## Teardown boundary

All billable-capable resources are kept in the exact resource group `rg-contractiq-ai-dev`. After preserving any required screenshots and validation evidence, delete that resource group from the Azure portal or with an explicitly reviewed CLI command. The subscription-level budget can then be removed separately.

Deletion is intentionally not automated from a pull request or a normal CI run.
