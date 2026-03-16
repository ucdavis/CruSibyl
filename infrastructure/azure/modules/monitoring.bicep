@description('Azure region for monitoring resources.')
param location string

@description('Tags to apply to monitoring resources.')
param tags object

@description('Application Insights resource name.')
param appInsightsName string

@description('Log Analytics workspace name.')
param logAnalyticsWorkspaceName string

@minValue(30)
@maxValue(730)
@description('Workspace retention in days.')
param retentionInDays int = 30

resource logAnalyticsWorkspace 'Microsoft.OperationalInsights/workspaces@2025-07-01' = {
  name: logAnalyticsWorkspaceName
  location: location
  tags: tags
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: retentionInDays
  }
}

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: appInsightsName
  location: location
  kind: 'web'
  tags: tags
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalyticsWorkspace.id
  }
}

output appInsightsName string = appInsights.name
output logAnalyticsWorkspaceName string = logAnalyticsWorkspace.name
output appInsightsConnectionString string = appInsights.properties.ConnectionString
output appInsightsInstrumentationKey string = appInsights.properties.InstrumentationKey
