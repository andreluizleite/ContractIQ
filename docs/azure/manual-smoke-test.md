# Manual keyless Azure AI smoke test

## Purpose

The `Azure AI smoke test` GitHub Actions workflow proves that the application-owned Foundry embedding adapter and Azure AI Search hybrid adapter can work together with Microsoft Entra authentication. It is deliberately separate from normal CI because it contacts live Azure resources.

The workflow is safe by construction:

- it runs only through `workflow_dispatch`;
- the operator must explicitly select the `azure-dev` GitHub environment;
- GitHub obtains a short-lived Azure token through OIDC workload identity federation;
- no Azure client secret or service key is stored in GitHub;
- the tool sends one embedding request containing exactly two short fictional inputs;
- it indexes exactly one fictional chunk and performs one hybrid search query;
- Foundry and Azure AI Search SDK retries are set to zero for this run;
- the tool times out after 90 seconds and the job after five minutes;
- concurrent runs for the same environment are serialized;
- the usage report contains counts and duration, not document or query content.

Normal `push` and `pull_request` CI remains offline and deterministic. Adding this workflow does not provision Azure resources or deploy a model.

## What must exist first

Do not configure or run the workflow until the separately reviewed Azure deployment has created:

1. the Foundry account and project;
2. one compatible 768-dimension embedding deployment;
3. the Free Azure AI Search service;
4. the GitHub OIDC application/service principal and its role assignments.

The smoke tool creates or updates only the configured Search index. Use the dedicated name `contractiq-smoke-v1` so smoke data does not mix with the demo index.

## 1. Create the GitHub environment

In the GitHub repository:

1. Open **Settings > Environments**.
2. Create an environment named exactly `azure-dev`.
3. Restrict deployment branches and tags to the protected `main` branch.
4. Add yourself as a required reviewer if the GitHub plan supports it. This creates an additional human approval before a live run.
5. Do not add an Azure client secret.

The workflow's environment choice is intentionally limited to this exact name,
and the job also refuses to run from a ref other than `main`.

## 2. Create the federated identity

In **Azure portal > Microsoft Entra ID > App registrations**:

1. Create a single-tenant registration named `github-contractiq-azure-dev`.
2. Open **Certificates & secrets > Federated credentials**.
3. Choose the GitHub Actions deployment-environment scenario.
4. Configure:
   - organization: `andreluizleite`;
   - repository: `ContractIQ`;
   - entity type: `Environment`;
   - environment: `azure-dev`;
   - name: `github-contractiq-azure-dev`.
5. Confirm that the generated subject is exactly:

   ```text
   repo:andreluizleite/ContractIQ:environment:azure-dev
   ```

The trusted issuer is `https://token.actions.githubusercontent.com` and the audience is `api://AzureADTokenExchange`. No password is created.

Record these non-secret identifiers:

- application (client) ID;
- directory (tenant) ID;
- service principal object ID from the corresponding Enterprise Application.

The service principal object ID is the value passed to Bicep as `smokeTestPrincipalId`. Do not confuse it with the app-registration object ID.

## 3. Assign least-privilege roles

Pass the service principal object ID to the reviewed Bicep deployment:

```powershell
-smokeTestPrincipalId '<service-principal-object-id>'
```

The template scopes these roles to the individual resources:

| Resource | Role | Why it is required |
| --- | --- | --- |
| Foundry account | Cognitive Services OpenAI User | Invoke the embedding deployment |
| Azure AI Search service | Search Service Contributor | Create or update the versioned smoke index schema |
| Azure AI Search service | Search Index Data Contributor | Upload one chunk and run one hybrid query |

No Owner, Contributor, subscription-wide data role, or Microsoft Graph permission is required by the workflow.

## 4. Configure non-secret GitHub variables

Add the following variables to the `azure-dev` GitHub environment, not repository secrets:

| Variable | Value |
| --- | --- |
| `AZURE_CLIENT_ID` | App registration application/client ID |
| `AZURE_TENANT_ID` | Entra directory/tenant ID |
| `AZURE_SUBSCRIPTION_ID` | Approved Azure subscription ID |
| `FOUNDRY_OPENAI_ENDPOINT` | Bicep `foundryOpenAIEndpoint` output ending in `/openai/v1/` |
| `FOUNDRY_EMBEDDING_DEPLOYMENT` | Approved 768-dimension embedding deployment name |
| `AZURE_SEARCH_ENDPOINT` | Bicep `searchEndpoint` output |
| `AZURE_SEARCH_INDEX_NAME` | `contractiq-smoke-v1` |

These identifiers and endpoints are configuration, not authentication secrets. Authentication succeeds only when GitHub presents an OIDC token whose repository and environment claims match the federated credential.

## 5. Run and review

1. Open **Actions > Azure AI smoke test**.
2. Select **Run workflow**.
3. Keep the environment set to `azure-dev`.
4. Approve the environment gate if one was configured.
5. Review the job summary and the seven-day `azure-ai-smoke-test-report` artifact.

A successful report must show:

- embedding requests: `1`;
- embedding inputs: `2`;
- indexed chunks: `1`;
- search queries: `1`;
- search results: `1`.

The input-character count, model name, index name, result count, and duration are reported because they help explain consumption. Exact provider cost is not estimated by the workflow.

## Failure behavior

The workflow does not retry the model or Search operation. A failure ends the run and requires a person to inspect configuration before manually starting another run. Common causes are:

- the federated subject does not use the `azure-dev` environment;
- the service principal object ID was not supplied to Bicep;
- a role assignment has not finished propagating;
- the Foundry endpoint does not end in `/openai/v1/`;
- the embedding deployment name or 768-dimension configuration is wrong;
- the Free Search service or dedicated index is unavailable.

Error output names the failed area but does not print tokens, document content, or provider response bodies.

## Revoke access

To stop future workflow access without deleting Azure resources:

1. Delete the `github-contractiq-azure-dev` federated credential from the Entra app registration, or disable/delete the corresponding Enterprise Application.
2. Remove the seven GitHub environment variables from `azure-dev`.
3. Remove the optional `smokeTestPrincipalId` from the next Bicep deployment so its three role assignments are deleted.
4. Delete the `azure-dev` GitHub environment if it will not be reused.

For complete project teardown, delete the isolated `rg-contractiq-ai-dev` resource group after preserving the required portfolio evidence, then remove the subscription-level budget separately. Resource deletion remains an explicit human action and is never performed by this workflow.
