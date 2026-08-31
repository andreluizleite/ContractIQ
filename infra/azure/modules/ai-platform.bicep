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

var foundryAccountName = 'aif-contractiq-${environmentName}-${uniqueSuffix}'
var foundryProjectName = 'contractiq-${environmentName}'
var searchServiceName = 'srch-contractiq-${environmentName}-${uniqueSuffix}'

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

resource searchService 'Microsoft.Search/searchServices@2025-05-01' = {
  name: searchServiceName
  location: location
  sku: {
    name: 'free'
  }
  identity: {
    type: 'SystemAssigned'
  }
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
    semanticSearch: 'free'
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

output foundryAccountName string = foundryAccount.name
output foundryProjectName string = foundryProject.name
output foundryEndpoint string = foundryAccount.properties.endpoint
output searchServiceName string = searchService.name
output searchEndpoint string = 'https://${searchService.name}.search.windows.net'
