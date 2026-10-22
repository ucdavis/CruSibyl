@description('Azure region for compute resources.')
param webAppLocation string

@description('Tags to apply to compute resources.')
param tags object

@description('Existing shared Linux App Service plan resource ID.')
param webPlanId string

@description('Web runtime stack.')
param linuxFxVersion string = 'DOTNETCORE|10.0'

@secure()
param existingWebSettings object = {}

@secure()
param existingFunctionSettings object = {}

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

resource webApp 'Microsoft.Web/sites@2025-03-01' = {
  name: webAppName
  location: webAppLocation
  kind: 'app,linux'
  identity: {
    type: 'SystemAssigned'
  }
  tags: tags
  properties: {
    serverFarmId: webPlanId
    httpsOnly: true
    siteConfig: {
      alwaysOn: true
      healthCheckPath: '/health'
      http20Enabled: true
      appCommandLine: 'dotnet /home/site/wwwroot/CruSibyl.Web.dll'
      ftpsState: 'FtpsOnly'
      linuxFxVersion: linuxFxVersion
      minTlsVersion: '1.2'
      appSettings: concat(map(items(existingWebSettings), setting => { name: setting.key, value: string(setting.value) }), [
        {
          name: 'WEBSITE_RUN_FROM_PACKAGE'
          value: '1'
        }
        {
          name: 'ASPNETCORE_ENVIRONMENT'
          value: environmentName == 'prod' ? 'Production' : 'Staging'
        }
        {
          name: 'Serilog__Environment'
          value: environmentName
        }
        {
          name: 'Azure__SubscriptionId'
          value: subscription().subscriptionId
        }
        {
          name: 'WEBSITES_CONTAINER_START_TIME_LIMIT'
          value: '600'
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

resource webConnectionStrings 'Microsoft.Web/sites/config@2025-03-01' = {
  parent: webApp
  name: 'connectionstrings'
  properties: {
    DefaultConnection: {
      value: sqlConnectionString
      type: 'SQLAzure'
    }
  }
}

resource functionApp 'Microsoft.Web/sites@2025-03-01' = {
  name: functionAppName
  location: webAppLocation
  kind: 'functionapp,linux'
  identity: {
    type: 'SystemAssigned'
  }
  tags: tags
  properties: {
    serverFarmId: webPlanId
    httpsOnly: true
    siteConfig: {
      alwaysOn: true
      ftpsState: 'FtpsOnly'
      linuxFxVersion: 'DOTNET-ISOLATED|10.0'
      minTlsVersion: '1.2'
      cors: {
        allowedOrigins: functionAppCorsAllowedOrigins
        supportCredentials: false
      }
      appSettings: concat(map(items(existingFunctionSettings), setting => { name: setting.key, value: string(setting.value) }), [
        {
          name: 'WEBSITE_RUN_FROM_PACKAGE'
          value: '1'
        }
        {
          name: 'AZURE_FUNCTIONS_ENVIRONMENT'
          value: environmentName == 'prod' ? 'Production' : 'Staging'
        }
        {
          name: 'AzureWebJobsStorage'
          value: functionStorageConnectionString
        }
        {
          name: 'Serilog__Environment'
          value: environmentName
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

resource functionConnectionStrings 'Microsoft.Web/sites/config@2025-03-01' = {
  parent: functionApp
  name: 'connectionstrings'
  properties: {
    DefaultConnection: {
      value: sqlConnectionString
      type: 'SQLAzure'
    }
  }
}

output defaultHostName string = webApp.properties.defaultHostName
output principalId string = webApp.identity.principalId
output webAppName string = webApp.name
output functionAppName string = functionApp.name
output functionPrincipalId string = functionApp.identity.principalId!
