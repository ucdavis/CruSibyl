targetScope = 'resourceGroup'

@description('Base application name used when deriving resource names.')
param appName string = 'crusibyl'

@allowed([
  'dev'
  'test'
  'prod'
])
@description('Deployment environment name.')
param env string = 'test'

@description('Azure region for deployed resources.')
param location string = resourceGroup().location

@description('Whether to deploy Log Analytics + Application Insights.')
param deployMonitoring bool = true

@description('Additional tags to apply to resources.')
param tags object = {}

@description('SQL admin login for Azure SQL authentication.')
param sqlAdminLogin string

@secure()
@description('SQL admin password for Azure SQL authentication.')
param sqlAdminPassword string

@description('SQL database name.')
param sqlDatabaseName string = appName

@description('Shared App Service plan SKU name.')
param planSkuName string = 'B1'

@description('Shared App Service plan SKU tier.')
param planSkuTier string = 'Basic'

@description('Shared App Service plan capacity.')
param planCapacity int = 1

@description('Optional explicit App Service plan name override.')
param appServicePlanName string = ''

@description('Optional explicit Web App name override.')
param webAppName string = ''

@description('Optional explicit Function App name override.')
param functionAppName string = ''

@description('Optional explicit Function storage account name override.')
param functionStorageAccountName string = ''

@description('Optional explicit SQL server name override.')
param sqlServerName string = ''

@description('Optional explicit Application Insights name override.')
param appInsightsName string = ''

@description('Optional explicit Log Analytics workspace name override.')
param logAnalyticsWorkspaceName string = ''

@minValue(30)
@maxValue(730)
@description('Log Analytics retention in days when monitoring is enabled.')
param monitoringRetentionInDays int = 30

@description('Whether to create a resource-group Reader role assignment for the Function App identity.')
param assignResourceGroupReader bool = false

var appSlug = toLower(replace(replace(replace(appName, '-', ''), '_', ''), ' ', ''))
var envSlug = toLower(replace(replace(env, '-', ''), ' ', ''))
var nameToken = substring(uniqueString(resourceGroup().id, appName, env), 0, 6)

var resolvedAppServicePlanName = appServicePlanName == '' ? toLower('asp-${appSlug}-${envSlug}-${nameToken}') : appServicePlanName
var resolvedWebAppName = webAppName == '' ? toLower('web-${appSlug}-${envSlug}-${nameToken}') : toLower(webAppName)
var resolvedFunctionAppName = functionAppName == '' ? toLower('fn-${appSlug}-${envSlug}-${nameToken}') : toLower(functionAppName)

var storageNameToken = nameToken
var functionStoragePrefix = take('st${appSlug}${envSlug}fn', 24 - length(storageNameToken))
var resolvedFunctionStorageAccountName = functionStorageAccountName == '' ? '${functionStoragePrefix}${storageNameToken}' : toLower(functionStorageAccountName)

var resolvedSqlServerName = sqlServerName == '' ? toLower('sql-${appSlug}-${envSlug}-${nameToken}') : toLower(sqlServerName)
var resolvedAppInsightsName = appInsightsName == '' ? toLower('appi-${appSlug}-${envSlug}-${nameToken}') : appInsightsName
var resolvedLogAnalyticsWorkspaceName = logAnalyticsWorkspaceName == '' ? toLower('log-${appSlug}-${envSlug}-${nameToken}') : logAnalyticsWorkspaceName

var resourceTags = union(tags, {
  application: appName
  environment: env
})

module functionStorage 'modules/function-storage.bicep' = {
  name: 'functionStorage'
  params: {
    name: resolvedFunctionStorageAccountName
    location: location
    tags: resourceTags
  }
}

module monitoring 'modules/monitoring.bicep' = if (deployMonitoring) {
  name: 'monitoring'
  params: {
    appInsightsName: resolvedAppInsightsName
    location: location
    logAnalyticsWorkspaceName: resolvedLogAnalyticsWorkspaceName
    retentionInDays: monitoringRetentionInDays
    tags: resourceTags
  }
}

module sql 'modules/sql.bicep' = {
  name: 'sql'
  params: {
    adminLogin: sqlAdminLogin
    adminPassword: sqlAdminPassword
    databaseName: sqlDatabaseName
    location: location
    name: resolvedSqlServerName
    tags: resourceTags
  }
}

var sqlServerHostnameSuffix = environment().suffixes.sqlServerHostname
var sqlServerFqdn = '${resolvedSqlServerName}${startsWith(sqlServerHostnameSuffix, '.') ? '' : '.'}${sqlServerHostnameSuffix}'
var sqlConnectionString = 'Server=tcp:${sqlServerFqdn},1433;Initial Catalog=${sqlDatabaseName};Persist Security Info=False;User ID=${sqlAdminLogin};Password=${sqlAdminPassword};MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;'

module compute 'modules/compute.bicep' = {
  name: 'compute'
  params: {
    appInsightsConnectionString: deployMonitoring ? monitoring!.outputs.appInsightsConnectionString : ''
    appInsightsInstrumentationKey: deployMonitoring ? monitoring!.outputs.appInsightsInstrumentationKey : ''
    environmentName: env
    functionAppName: resolvedFunctionAppName
    functionStorageConnectionString: functionStorage.outputs.connectionString
    location: location
    planCapacity: planCapacity
    planName: resolvedAppServicePlanName
    planSkuName: planSkuName
    planSkuTier: planSkuTier
    sqlConnectionString: sqlConnectionString
    tags: resourceTags
    webAppName: resolvedWebAppName
  }
}

module rbac 'modules/rbac.bicep' = {
  name: 'rbac'
  params: {
    assignResourceGroupReader: assignResourceGroupReader
    functionPrincipalId: compute.outputs.functionPrincipalId
  }
}

output appServicePlanName string = resolvedAppServicePlanName
output webAppName string = resolvedWebAppName
output functionAppName string = resolvedFunctionAppName
output functionStorageAccountName string = functionStorage.outputs.accountName
output sqlServerName string = sql.outputs.serverName
output sqlDatabaseName string = sqlDatabaseName
output appInsightsName string = deployMonitoring ? monitoring!.outputs.appInsightsName : ''
output logAnalyticsWorkspaceName string = deployMonitoring ? monitoring!.outputs.logAnalyticsWorkspaceName : ''
output functionPrincipalId string = compute.outputs.functionPrincipalId
