@description('Azure region for compute resources.')
param location string

@description('Tags to apply to compute resources.')
param tags object

@description('Shared App Service plan name.')
param planName string

@description('Shared App Service plan SKU name.')
param planSkuName string = 'B1'

@description('Shared App Service plan SKU tier.')
param planSkuTier string = 'Basic'

@description('Shared App Service plan capacity.')
param planCapacity int = 1

@description('Web App name.')
param webAppName string

@description('Function App name.')
param functionAppName string

@secure()
@description('Function host storage connection string.')
param functionStorageConnectionString string

@secure()
@description('SQL connection string.')
param sqlConnectionString string

@description('Environment name passed into app settings.')
param environmentName string

@description('Application Insights connection string (optional).')
param appInsightsConnectionString string = ''

@description('Application Insights instrumentation key (optional).')
param appInsightsInstrumentationKey string = ''

@description('Allowed CORS origins for the Function App.')
param functionAppCorsAllowedOrigins array = [
  'https://portal.azure.com'
]

resource sharedPlan 'Microsoft.Web/serverfarms@2025-03-01' = {
  name: planName
  location: location
  kind: 'linux'
  sku: {
    name: planSkuName
    tier: planSkuTier
    size: planSkuName
    capacity: planCapacity
  }
  tags: tags
  properties: {
    reserved: true
  }
}

resource webApp 'Microsoft.Web/sites@2025-03-01' = {
  name: webAppName
  location: location
  kind: 'app,linux'
  tags: tags
  properties: {
    serverFarmId: sharedPlan.id
    httpsOnly: true
    siteConfig: {
      alwaysOn: true
      ftpsState: 'FtpsOnly'
      linuxFxVersion: 'DOTNETCORE|8.0'
      minTlsVersion: '1.2'
      appSettings: concat([
        {
          name: 'ASPNETCORE_ENVIRONMENT'
          value: environmentName
        }
        {
          name: 'ConnectionStrings__DefaultConnection'
          value: sqlConnectionString
        }
        {
          name: 'Azure__SubscriptionId'
          value: subscription().subscriptionId
        }
      ], appInsightsConnectionString != '' ? [
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: appInsightsConnectionString
        }
        {
          name: 'APPINSIGHTS_INSTRUMENTATIONKEY'
          value: appInsightsInstrumentationKey
        }
      ] : [])
    }
  }
}

resource functionApp 'Microsoft.Web/sites@2025-03-01' = {
  name: functionAppName
  location: location
  kind: 'functionapp,linux'
  identity: {
    type: 'SystemAssigned'
  }
  tags: tags
  properties: {
    serverFarmId: sharedPlan.id
    httpsOnly: true
    siteConfig: {
      alwaysOn: true
      ftpsState: 'FtpsOnly'
      linuxFxVersion: 'DOTNET-ISOLATED|8.0'
      minTlsVersion: '1.2'
      cors: {
        allowedOrigins: functionAppCorsAllowedOrigins
        supportCredentials: false
      }
      appSettings: concat([
        {
          name: 'AZURE_FUNCTIONS_ENVIRONMENT'
          value: environmentName
        }
        {
          name: 'AzureWebJobsStorage'
          value: functionStorageConnectionString
        }
        {
          name: 'ConnectionStrings__DefaultConnection'
          value: sqlConnectionString
        }
        {
          name: 'Azure__SubscriptionId'
          value: subscription().subscriptionId
        }
        {
          name: 'FUNCTIONS_EXTENSION_VERSION'
          value: '~4'
        }
        {
          name: 'FUNCTIONS_WORKER_RUNTIME'
          value: 'dotnet-isolated'
        }
      ], appInsightsConnectionString != '' ? [
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: appInsightsConnectionString
        }
        {
          name: 'APPINSIGHTS_INSTRUMENTATIONKEY'
          value: appInsightsInstrumentationKey
        }
      ] : [])
    }
  }
}

output planName string = sharedPlan.name
output webAppName string = webApp.name
output functionAppName string = functionApp.name
output functionPrincipalId string = functionApp.identity.principalId!
