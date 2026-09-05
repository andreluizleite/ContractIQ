@description('Deployment environment name.')
param environmentName string

@description('Azure region shared by Foundry and Azure AI Search.')
param location string

@description('Deterministic suffix used for globally unique resource names.')
param uniqueSuffix string

@description('Tags inherited from the subscription deployment.')
param tags object

@description('Optional Microsoft Entra object ID for local keyless development.')
param developerPrincipalId string = ''

@description('Optional service principal object ID for the manual GitHub OIDC smoke test.')
param smokeTestPrincipalId string = ''

@description('Whether the validated pay-as-you-go model deployments are included.')
param deployModels bool = false

@description('Chat model name selected from the live catalog.')
param chatModelName string

@description('Pinned chat model version selected from the live catalog.')
param chatModelVersion string

@description('Embedding model name selected from the live catalog.')
param embeddingModelName string

@description('Pinned embedding model version selected from the live catalog.')
param embeddingModelVersion string

@description('GlobalStandard chat capacity in thousands of tokens per minute.')
param chatModelCapacity int

@description('GlobalStandard embedding capacity in thousands of tokens per minute.')
param embeddingModelCapacity int

var foundryAccountName = 'aif-contractiq-${environmentName}-${uniqueSuffix}'
var foundryProjectName = 'contractiq-${environmentName}'
var searchServiceName = 'srch-contractiq-${environmentName}-${uniqueSuffix}'
var chatDeploymentName = 'contractiq-chat'
var embeddingDeploymentName = 'contractiq-embeddings'

var cognitiveServicesOpenAiUserRoleId = subscriptionResourceId(
  'Microsoft.Authorization/roleDefinitions',
  '5e0bd9bd-7b93-4f28-af87-19fc36ad61bd'
)
var searchServiceContributorRoleId = subscriptionResourceId(
  'Microsoft.Authorization/roleDefinitions',
  '7ca78c08-252a-4471-8644-bb5ff32d4ba0'
)
var searchIndexDataContributorRoleId = subscriptionResourceId(
  'Microsoft.Authorization/roleDefinitions',
  '8ebe5a00-799e-43f5-93ac-243d3dce84a7'
)

resource foundryAccount 'Microsoft.CognitiveServices/accounts@2025-06-01' = {
  name: foundryAccountName
  location: location
  kind: 'AIServices'
  sku: {
    name: 'S0'
  }
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    allowProjectManagement: true
    customSubDomainName: foundryAccountName
    disableLocalAuth: true
    dynamicThrottlingEnabled: false
    publicNetworkAccess: 'Enabled'
    restrictOutboundNetworkAccess: false
  }
  tags: tags
}

resource foundryProject 'Microsoft.CognitiveServices/accounts/projects@2025-06-01' = {
  name: foundryProjectName
  parent: foundryAccount
  location: location
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    displayName: 'ContractIQ ${environmentName}'
    description: 'Optional Microsoft Foundry project for the ContractIQ portfolio application.'
  }
  tags: tags
}

resource chatDeployment 'Microsoft.CognitiveServices/accounts/deployments@2024-10-01' = if (deployModels) {
  name: chatDeploymentName
  parent: foundryAccount
  // Cognitive Services can reject concurrent child writes on a newly created
  // account. Keep project -> embeddings -> chat deterministic and idempotent.
  dependsOn: [
    embeddingDeployment
  ]
  sku: {
    name: 'GlobalStandard'
    capacity: chatModelCapacity
  }
  properties: {
    model: {
      format: 'OpenAI'
      name: chatModelName
      version: chatModelVersion
    }
  }
}

resource embeddingDeployment 'Microsoft.CognitiveServices/accounts/deployments@2024-10-01' = if (deployModels) {
  name: embeddingDeploymentName
  parent: foundryAccount
  dependsOn: [
    foundryProject
  ]
  sku: {
    name: 'GlobalStandard'
    capacity: embeddingModelCapacity
  }
  properties: {
    model: {
      format: 'OpenAI'
      name: embeddingModelName
      version: embeddingModelVersion
    }
  }
}

resource searchService 'Microsoft.Search/searchServices@2025-05-01' = {
  name: searchServiceName
  location: location
  sku: {
    name: 'free'
  }
  // The Free tier accepts inbound Entra/RBAC calls, but it cannot use a
  // Search-managed identity for outbound connections. ContractIQ generates
  // embeddings in the application and pushes them to Search, so no outbound
  // identity or integrated vectorizer is required for this portfolio profile.
  properties: {
    disableLocalAuth: true
    hostingMode: 'Default'
    networkRuleSet: {
      bypass: 'None'
      ipRules: []
    }
    partitionCount: 1
    publicNetworkAccess: 'Enabled'
    replicaCount: 1
  }
  tags: tags
}

resource foundryInferenceRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (!empty(developerPrincipalId)) {
  name: guid(foundryAccount.id, developerPrincipalId, cognitiveServicesOpenAiUserRoleId)
  scope: foundryAccount
  properties: {
    principalId: developerPrincipalId
    principalType: 'User'
    roleDefinitionId: cognitiveServicesOpenAiUserRoleId
  }
}

resource searchManagementRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (!empty(developerPrincipalId)) {
  name: guid(searchService.id, developerPrincipalId, searchServiceContributorRoleId)
  scope: searchService
  properties: {
    principalId: developerPrincipalId
    principalType: 'User'
    roleDefinitionId: searchServiceContributorRoleId
  }
}

resource searchDataRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (!empty(developerPrincipalId)) {
  name: guid(searchService.id, developerPrincipalId, searchIndexDataContributorRoleId)
  scope: searchService
  properties: {
    principalId: developerPrincipalId
    principalType: 'User'
    roleDefinitionId: searchIndexDataContributorRoleId
  }
}

resource smokeFoundryInferenceRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (!empty(smokeTestPrincipalId)) {
  name: guid(foundryAccount.id, smokeTestPrincipalId, cognitiveServicesOpenAiUserRoleId)
  scope: foundryAccount
  properties: {
    principalId: smokeTestPrincipalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: cognitiveServicesOpenAiUserRoleId
  }
}

resource smokeSearchManagementRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (!empty(smokeTestPrincipalId)) {
  name: guid(searchService.id, smokeTestPrincipalId, searchServiceContributorRoleId)
  scope: searchService
  properties: {
    principalId: smokeTestPrincipalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: searchServiceContributorRoleId
  }
}

resource smokeSearchDataRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (!empty(smokeTestPrincipalId)) {
  name: guid(searchService.id, smokeTestPrincipalId, searchIndexDataContributorRoleId)
  scope: searchService
  properties: {
    principalId: smokeTestPrincipalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: searchIndexDataContributorRoleId
  }
}

output foundryAccountName string = foundryAccount.name
output foundryProjectName string = foundryProject.name
output foundryEndpoint string = foundryAccount.properties.endpoint
output foundryOpenAIEndpoint string = 'https://${foundryAccount.name}.openai.azure.com/openai/v1/'
output chatDeploymentName string = chatDeploymentName
output embeddingDeploymentName string = embeddingDeploymentName
output searchServiceName string = searchService.name
output searchEndpoint string = 'https://${searchService.name}.search.windows.net'
