# CruSibyl Azure Infrastructure

This folder contains the initial Bicep scaffold for CruSibyl's Azure infrastructure.

## Current scaffold

The scaffold currently covers:

- a shared Linux App Service plan
- a Web App
- a Function App on the shared plan with `Always On`
- Function host storage
- Azure SQL server + database
- same-subscription RBAC for the Function App identity to read App Services and call Kudu/WebJobs APIs
- optional Log Analytics + Application Insights
- Azure Pipelines deploy-stage template for infra + app deployment

The scaffold intentionally does not yet cover:

- app-specific secret values themselves
- timer diagnostics in `PackageVersionSyncFunction`

## Deploy

Prerequisites:

- Azure CLI installed
- authenticated with `az login`
- access to the target subscription

Set the SQL admin login and password, then deploy:

```bash
export SQL_ADMIN_LOGIN='your-existing-sql-admin-login'
export SQL_ADMIN_PASSWORD='your-strong-password'
bash infrastructure/azure/scripts/deploy_test.sh
```

Generic deploy entrypoint:

```bash
export RESOURCE_GROUP='CruSibyl-Test'
export LOCATION='westus2'
export APP_NAME='crusibyl'
export ENVIRONMENT='test'
export DEPLOYMENT_NAME='crusibyl-test'
export SQL_ADMIN_LOGIN='your-existing-sql-admin-login'
export SQL_ADMIN_PASSWORD='your-strong-password'
bash infrastructure/azure/scripts/deploy.sh
```

Optional overrides:

- `DEPLOY_MONITORING=false`
- `APP_SERVICE_PLAN_SKU=B1`
- `APP_SERVICE_PLAN_TIER=Basic`
- `WEB_APP_NAME=...`
- `FUNCTION_APP_NAME=...`
- `APP_SERVICE_PLAN_NAME=...`
- `FUNCTION_STORAGE_ACCOUNT_NAME=...`
- `SQL_SERVER_NAME=...`
- `AZURE_SYNC_SUBSCRIPTION_ROLE=WebsiteContributor`

## Azure Pipelines

The root `azure-pipelines.yml` now follows the same general pattern as `../readable`:

- build both deployable apps
- publish a single `packages` artifact with `webapp.zip` and `functionapp.zip`
- deploy through a reusable stage template at `infrastructure/azure/pipelines/templates/deploy-stage.yml`
- optionally deploy or update infrastructure first with `deployInfra=true`
- resolve deployed app names from Bicep outputs when available
- validate that the Function App host is running and expected functions are registered after deploy

Suggested Azure DevOps variable groups:

- `crusibyl-test`
- `crusibyl-prod`

Recommended variable-group values:

- `RESOURCE_GROUP`
- `LOCATION`
- `APP_NAME`
- `ENVIRONMENT`
- `DEPLOYMENT_NAME`
- `WEB_APP_NAME`
- `FUNCTION_APP_NAME`
- `SQL_ADMIN_LOGIN`
- `SQL_ADMIN_PASSWORD`

Optional variable-group values:

- `deployInfra`
- `DEPLOY_MONITORING`
- `APP_SERVICE_PLAN_SKU`
- `APP_SERVICE_PLAN_TIER`
- `APP_SERVICE_PLAN_CAPACITY`
- `SQL_DATABASE_NAME`
- `APP_SERVICE_PLAN_NAME`
- `FUNCTION_STORAGE_ACCOUNT_NAME`
- `SQL_SERVER_NAME`
- `APP_INSIGHTS_NAME`
- `LOG_ANALYTICS_WORKSPACE_NAME`
- `AZURE_SYNC_SUBSCRIPTION_ROLE`
- `WEB_APP_SETTINGS_JSON`
- `FUNCTION_APP_SETTINGS_JSON`
- `EXPECTED_FUNCTIONS`

Notes:

- `deployInfra` defaults to `false` in the pipeline. Set it to `true` in a variable group or at queue time when you want the stage to run the Bicep deployment before code deploy.
- `WEB_APP_NAME` and `FUNCTION_APP_NAME` are only strictly required when `deployInfra=false` and the pipeline cannot look up names from a prior Bicep deployment output.
- `SQL_ADMIN_LOGIN` and `SQL_ADMIN_PASSWORD` are only required when `deployInfra=true`.
- `WEB_APP_SETTINGS_JSON` and `FUNCTION_APP_SETTINGS_JSON` default to `[]` in the pipeline and are meant to hold Azure App Service settings payloads.
- `EXPECTED_FUNCTIONS` defaults to the current timer functions: `AppWebJobSyncFunction,ManifestSyncFunction,PackageVersionSyncFunction,WebJobStatusSyncFunction`.
- `AZURE_SYNC_SUBSCRIPTION_ROLE` defaults to `WebsiteContributor`, which is needed for Kudu/WebJobs API access. Use `Reader` only for deployments that do not call Kudu.
- Bicep assigns Azure sync access only in the deployment subscription. Test and production isolation is handled by separate deployments, databases, service connections, and Function App identities.
- The identity running the Bicep deployment must be allowed to create role assignments, such as Owner or User Access Administrator, in the deployment subscription.

Example `WEB_APP_SETTINGS_JSON` value:

```json
[
  { "name": "Authentication__ClientId", "value": "$(AUTHENTICATION_CLIENT_ID)", "slotSetting": false },
  { "name": "Authentication__ClientSecret", "value": "$(AUTHENTICATION_CLIENT_SECRET)", "slotSetting": false },
  { "name": "Authentication__Authority", "value": "https://cas.ucdavis.edu/cas/oidc", "slotSetting": false },
  { "name": "Authentication__IamKey", "value": "$(AUTHENTICATION_IAM_KEY)", "slotSetting": false },
  { "name": "Azure__SubscriptionId", "value": "$(AZURE_SUBSCRIPTION_ID)", "slotSetting": false },
  { "name": "Serilog__Environment", "value": "$(ENVIRONMENT)", "slotSetting": false },
  { "name": "Serilog__ElasticUrl", "value": "$(SERILOG_ELASTIC_URL)", "slotSetting": false }
]
```

Example `FUNCTION_APP_SETTINGS_JSON` value:

```json
[
  { "name": "GitHub__RepoOwner", "value": "ucdavis", "slotSetting": false },
  { "name": "GitHub__AccessToken", "value": "$(GITHUB_ACCESS_TOKEN)", "slotSetting": false },
  { "name": "Azure__SubscriptionId", "value": "$(AZURE_SUBSCRIPTION_ID)", "slotSetting": false },
  { "name": "Serilog__Environment", "value": "$(ENVIRONMENT)", "slotSetting": false },
  { "name": "Serilog__ElasticUrl", "value": "$(SERILOG_ELASTIC_URL)", "slotSetting": false }
]
```

## Notes

- Resource names default to deterministic values derived from app name, environment, and resource group. The Web App defaults are `crusibyl-test` for test and `crusibyl` for prod unless explicitly overridden.
- Monitoring is enabled by default and can be disabled with `DEPLOY_MONITORING=false`.
- The Web App and Function App store `DefaultConnection` in App Service connection strings as a `SQLAzure` connection string. Remove stale `ConnectionStrings__DefaultConnection` app settings if they were previously configured manually or by pipeline variables.
- The Function App uses its system-assigned managed identity for Azure sync calls. Bicep grants that identity `AZURE_SYNC_SUBSCRIPTION_ROLE` only in the deployment subscription.
- The Function App allows `https://portal.azure.com` in CORS so HTTP-trigger functions can be invoked from the Azure portal.
- The pipeline template validates host health plus registered function names, which is our first-pass check that timer triggers synced after deployment.
