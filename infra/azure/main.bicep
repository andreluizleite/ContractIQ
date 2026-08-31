targetScope = 'subscription'

@description('Short environment name used in resource names and tags.')
@allowed([
  'dev'
  'test'
])
param environmentName string = 'dev'

@description('Azure region selected only after Foundry model capacity and Azure AI Search availability are checked.')
param location string

@description('Monthly cost alert target. A budget reports spend but does not stop resources.')
@minValue(1)
param budgetAmount int = 10

@description('Email addresses that receive the 50%, 80%, and 100% budget alerts.')
param budgetContactEmails array = []

@description('First day of the budget period. Override this value when reusing the template in a later month.')
param budgetStartDate string = utcNow('yyyy-MM-01')

@description('Optional Microsoft Entra object ID for the local developer. Leave empty during validation.')
param developerPrincipalId string = ''

@description('Additional tags applied to every provisioned resource.')
param tags object = {}

var resourceGroupName = 'rg-contractiq-ai-${environmentName}'
var uniqueSuffix = take(uniqueString(subscription().id, environmentName), 6)
var commonTags = union(
  {
    application: 'ContractIQ'
    environment: environmentName
    managedBy: 'Bicep'
    purpose: 'portfolio-ai'
  },
  tags
)

resource resourceGroup 'Microsoft.Resources/resourceGroups@2024-03-01' = {
  name: resourceGroupName
  location: location
  tags: commonTags
}

resource monthlyBudget 'Microsoft.Consumption/budgets@2023-11-01' = {
  name: 'budget-contractiq-ai-${environmentName}'
  properties: {
    amount: budgetAmount
    category: 'Cost'
    timeGrain: 'Monthly'
    timePeriod: {
      startDate: budgetStartDate
      endDate: dateTimeAdd(budgetStartDate, 'P1Y')
    }
    notifications: length(budgetContactEmails) > 0
      ? {
          Actual50Percent: {
            enabled: true
            operator: 'GreaterThanOrEqualTo'
            threshold: 50
            thresholdType: 'Actual'
            contactEmails: budgetContactEmails
            contactGroups: []
            contactRoles: []
          }
          Actual80Percent: {
            enabled: true
            operator: 'GreaterThanOrEqualTo'
            threshold: 80
            thresholdType: 'Actual'
            contactEmails: budgetContactEmails
            contactGroups: []
            contactRoles: []
          }
          Actual100Percent: {
            enabled: true
            operator: 'GreaterThanOrEqualTo'
            threshold: 100
            thresholdType: 'Actual'
            contactEmails: budgetContactEmails
            contactGroups: []
            contactRoles: []
          }
        }
      : {}
  }
}

module aiPlatform 'modules/ai-platform.bicep' = {
  name: 'contractiq-ai-platform-${environmentName}'
  scope: resourceGroup
  params: {
    developerPrincipalId: developerPrincipalId
    environmentName: environmentName
    location: location
    tags: commonTags
    uniqueSuffix: uniqueSuffix
  }
}

output resourceGroupName string = resourceGroup.name
output foundryAccountName string = aiPlatform.outputs.foundryAccountName
output foundryProjectName string = aiPlatform.outputs.foundryProjectName
output foundryEndpoint string = aiPlatform.outputs.foundryEndpoint
output foundryOpenAIEndpoint string = aiPlatform.outputs.foundryOpenAIEndpoint
output searchServiceName string = aiPlatform.outputs.searchServiceName
output searchEndpoint string = aiPlatform.outputs.searchEndpoint
