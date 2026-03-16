targetScope = 'resourceGroup'

@description('Function App managed identity principal ID.')
param functionPrincipalId string = ''

@description('Whether to assign Reader on the current resource group to the Function App identity.')
param assignResourceGroupReader bool = false

var readerRoleDefinitionId = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'acdd72a7-3385-48ef-bd42-f606fba81ae7')

resource resourceGroupReader 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (assignResourceGroupReader && functionPrincipalId != '') {
  name: guid(resourceGroup().id, functionPrincipalId, 'reader')
  properties: {
    principalId: functionPrincipalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: readerRoleDefinitionId
  }
}

output notes string = 'Cross-subscription Reader assignments for scanned subscriptions are intentionally left for a later step.'
