targetScope = 'subscription'

@allowed(['test', 'prod'])
param env string
param expectedSubscriptionId string
param resourceGroupName string = 'rg-crusibyl-${env}'
param functionAppName string

var guardPassed = !empty(expectedSubscriptionId) && toLower(subscription().subscriptionId) == toLower(expectedSubscriptionId) && endsWith(toLower(resourceGroupName), '-${env}')

resource functionApp 'Microsoft.Web/sites@2025-03-01' existing = {
  name: functionAppName
  scope: resourceGroup(resourceGroupName)
}

module syncRole 'modules/azure-sync-subscription-rbac.bicep' = if (guardPassed) {
  name: 'crusibyl-${env}-runtime-rbac'
  params: {
    functionPrincipalId: functionApp.identity.principalId
    roleDefinitionGuid: 'de139f84-1756-47ae-9be6-808fbbe84772'
    roleAssignmentNameSeed: 'WebsiteContributor'
  }
}

output deploymentGuardPassed bool = guardPassed
