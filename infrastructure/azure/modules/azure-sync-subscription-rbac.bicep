targetScope = 'subscription'

@description('Function App managed identity principal ID.')
param functionPrincipalId string

@description('Built-in role definition GUID assigned to the Function App identity at this subscription scope.')
param roleDefinitionGuid string

@description('Stable seed used to derive the role assignment name.')
param roleAssignmentNameSeed string = 'azure-sync'

resource azureSyncRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (functionPrincipalId != '') {
  name: guid(subscription().id, functionPrincipalId, roleDefinitionGuid, roleAssignmentNameSeed)
  properties: {
    principalId: functionPrincipalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roleDefinitionGuid)
  }
}

output subscriptionId string = subscription().subscriptionId
