# Optional Azure AI foundation

This folder contains the reviewed infrastructure boundary for ContractIQ's optional Azure profile. Merely cloning, building, or testing the repository does not deploy it.

## Intended resources

| Resource | Configuration | Cost behavior |
| --- | --- | --- |
| Resource group | One isolated development group | No charge |
| Subscription budget | USD 10 planning target with 50%, 80%, and 100% alerts | No charge; alerts do not stop spend |
| Microsoft Foundry account and project | `AIServices`, public development endpoint, local authentication disabled | Account/project have no committed throughput |
| Optional model deployments | `gpt-5-mini` at 10K TPM and `text-embedding-3-small` at 1K TPM, both `GlobalStandard` | No fixed deployment charge; inference is usage-based |
| Azure AI Search | Free tier, one shared service with a 50 MB limit | No charge while the free tier remains available and its limits are respected |
| Role assignments | Least-privilege roles for the local developer and optional GitHub OIDC service principal | No charge |

Model deployments are declared but disabled by default through `deployModels=false`.
The selected names, versions, SKU, capacity, and region were resolved against
the live catalog and subscription quota on 2026-08-31. They must be checked
again if the deployment is executed later or in another subscription.

The chat and embedding capacities are intentionally separate. A live agent
request includes its system instructions and application-owned tool schemas, so
the original 1K chat allocation rejected the bounded demo before inference with
HTTP 429. The 10K chat allocation fits that request while embeddings remain at
the minimum 1K allocation. These values limit throughput; they do not prepay
tokens. The USD 10 budget and zero-retry live validation boundary remain in
place.

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

### Exact reviewed teardown procedure

Do not run these deletion commands until the portfolio evidence has been
preserved and the owner has explicitly approved teardown. First verify the
active subscription and exact targets:

```powershell
az account show --query "{Name:name, SubscriptionId:id, TenantId:tenantId}" --output table
az resource list --resource-group rg-contractiq-ai-dev `
  --query "[].{Name:name, Type:type, Location:location}" `
  --output table
az consumption budget show `
  --budget-name budget-contractiq-ai-dev `
  --output table
```

The expected resource group contains only the ContractIQ Foundry account and
project, its two model deployments, Azure AI Search, and their role assignments.
The budget is subscription-scoped and therefore is not removed with the group.

After explicit approval, delete the isolated resource group and wait for Azure
to report that it no longer exists:

```powershell
az group delete --name rg-contractiq-ai-dev --yes --no-wait
az group wait --name rg-contractiq-ai-dev --deleted
```

Then remove the separate alert budget:

```powershell
az consumption budget delete --budget-name budget-contractiq-ai-dev
```

Finally verify both cleanup boundaries:

```powershell
az group exists --name rg-contractiq-ai-dev
az consumption budget show --budget-name budget-contractiq-ai-dev
```

The expected group result is `false`; the budget lookup should report that the
budget is not found. Record the UTC deletion date in issue #53. Deleting this
Azure group does not remove the local repository, PostgreSQL volume, Aspire
dashboard, GitHub repository, or local user secrets.
