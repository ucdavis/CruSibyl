# CruSibyl Bicep Migration Plan

## Goal

Move CruSibyl's Azure deployment to Bicep, using the structure and workflow in `../readable/infrastructure/azure` as the model, while only carrying forward the resources CruSibyl actually needs.

This plan is intentionally biased toward a clean rebuild rather than preserving every detail of the current `CruSibyl-Test` resource group. As of 2026-03-13, the test environment is safe to delete and recreate.

## Current Status

- [x] Inventory the current `CruSibyl-Test` resource group
- [x] Inventory app/runtime dependencies from the CruSibyl codebase
- [x] Inspect `../readable` Bicep layout, scripts, and pipeline approach
- [x] Decide the v1 networking model for Azure SQL
- [x] Decide the v1 monitoring approach
- [x] Decide the v1 secret management model
- [x] Decide the function hosting plan we want to standardize on
- [x] Scaffold CruSibyl Bicep files
- [x] Implement Bicep modules and deploy scripts
- [x] Update Azure Pipelines to deploy infra and code together
- [ ] Rebuild test from scratch and validate

## Locked Decisions

Decisions confirmed on 2026-03-13:

- Networking: v1 will use public Azure SQL with explicit firewall rules and no private endpoint / VNet dependency.
- Monitoring: v1 will include Application Insights and Log Analytics, but monitoring must be easy to disable with a simple deployment flag.
- Secrets: v1 will keep secrets in Azure DevOps variable groups and apply them through pipeline-managed app settings rather than introducing Key Vault immediately.
- Function hosting: v1 will place the Function App on the web app's App Service plan and enable `Always On` instead of using a separate Consumption plan.
- Deployment validation: post-deploy validation will explicitly check Azure Functions host health and registered functions before considering a deployment healthy.

## Live Azure Snapshot

Captured on 2026-03-13 from subscription `105dede4-4731-492e-8c28-5121226319b0` and resource group `CruSibyl-Test` in `westus2`.

Important note:

- The resource group is in `westus2`, but most existing resources are currently deployed in `westus`.
- The migration should choose one target region intentionally instead of reproducing that mismatch by accident.

### Current resources

- Web App: `CruSibyl-Test`
- Web App plan: `ASP-CruSibylTest-92f9` (`B1`, Linux)
- Function App: `CruSibyl-Functions-Test`
- Function plan: `ASP-CruSibylTest-a342` (`Y1`, Windows consumption)
- Storage account: `crusibyltest93a0`
- Azure SQL server: `crusibyl-test-server`
- Azure SQL database: `crusibyl-test-database`
- Application Insights: `CruSibyl-Functions-Test`
- Smart detector rule: `Failure Anomalies - CruSibyl-Functions-Test`
- VNet: `vnet-gbwyfory`
- SQL private endpoint: `endpoint-mdv2jw6dwrwre`
- Private DNS zone: `privatelink.database.windows.net`

### Current configuration notes

- Web app is Linux .NET 8, HTTPS-only, and VNet-integrated.
- Web app does not currently have a managed identity.
- Web app stores SQL connection strings as App Service connection strings:
  - `AZURE_SQL_CONNECTIONSTRING`
  - `DefaultConnection`
- Function app has a system-assigned managed identity.
- Function app is not VNet-integrated.
- Function app stores `DefaultConnection` as a connection string and uses app settings for host/runtime config.
- SQL public network access is still enabled.
- SQL firewall rules currently include:
  - `AllowAllWindowsAzureIps`
  - `Spruce Home`
  - `Spruce VPN`
- Function app managed identity currently has no visible RBAC assignments from `az role assignment list`.

### Interpretation

- The current SQL private endpoint path is only partially implemented, because the function app is not VNet-integrated and SQL public access is still enabled.
- The live resource group should be treated as a source of clues, not a design to copy 1:1.
- We should explicitly codify RBAC for the function app during the migration instead of assuming the current setup is correct.

## What The App Actually Needs

### Azure resources that appear required

- One Web App for `CruSibyl.Web`
- One App Service plan shared by the web app and function app
- One Function App for `CruSibyl.Functions`
- One storage account for Function host/runtime needs
- One Azure SQL logical server
- One Azure SQL database
- Monitoring/telemetry resources
  - Recommendation: Application Insights plus Log Analytics
- Managed identity on the function app
- RBAC allowing the function app to enumerate App Services/WebJobs in the subscriptions it scans

### External dependencies that are not Azure resources we will provision

- UC Davis CAS OIDC
- UC Davis IAM / IET services
- GitHub API
- NuGet and npm registries
- Optional Serilog Elasticsearch endpoint

### Config and secrets the apps require

- Web app:
  - `Authentication__ClientId`
  - `Authentication__ClientSecret`
  - `Authentication__Authority`
  - `Authentication__IamKey`
  - `ConnectionStrings__DefaultConnection` or App Service SQL connection string equivalent
  - `Azure__Subscriptions__*`
  - `Serilog__*`
- Function app:
  - `AzureWebJobsStorage`
  - `ConnectionStrings__DefaultConnection` or App Service SQL connection string equivalent
  - `GitHub__AccessToken`
  - `GitHub__RepoOwner`
  - `Azure__Subscriptions__*`
  - `Authentication__IamKey`
  - `Serilog__*`
  - Functions runtime settings

### Azure resources that do not appear required by code today

- Service Bus
- Event Grid
- Blob data storage beyond Function host storage
- Partial private networking for SQL

## Recommended v1 Target Architecture

### Recommendation

Build a smaller, more explicit baseline first:

- Resource group created by script if missing
- One Linux App Service plan shared by `CruSibyl.Web` and `CruSibyl.Functions`
- Web App for `CruSibyl.Web`
- Function App with system-assigned managed identity
- `Always On` enabled for the shared plan / function host runtime path
- Storage account dedicated to Function host/runtime
- Azure SQL server + database using SQL authentication for v1
- Shared monitoring resources for both apps
- Explicit app settings and connection strings
- Explicit RBAC for the function app identity at the subscription scopes it needs to scan

### Recommendation on networking

For v1, prefer **public Azure SQL with explicit firewall/RBAC** over carrying forward the current mixed public/private design.

Decision:

- Accepted for v1.

Why:

- The current private endpoint is not end-to-end because the function app is not VNet-integrated.
- CruSibyl is safe to rebuild, so simplicity and repeatability are worth more than preserving partial hardening.
- This keeps the first Bicep implementation smaller and easier to verify.

### Recommendation on monitoring

Add Application Insights and Log Analytics as first-class Bicep resources instead of relying on portal-created defaults.

Decision:

- Accepted for v1, with an easy off switch.
- The Bicep and deploy scripts should expose a boolean such as `deployMonitoring`.
- The pipeline should be able to turn monitoring on or off without changing templates by hand.

Why:

- The function app already has App Insights today.
- The web app currently does not appear to have equivalent monitoring.
- Telemetry should be reproducible after a delete/recreate cycle.

### Recommendation on secrets

For v1, keep secret injection simple:

- Infra in Bicep
- Secrets in Azure DevOps variable groups plus an app-settings step in the pipeline

Decision:

- Accepted for v1.

Optional phase 2:

- Move secrets into Key Vault and use Key Vault references from the app services

### Recommendation on function hosting

Use the web app's App Service plan for the Function App and enable `Always On`.

Decision:

- Accepted for v1.
- Do not preserve the current separate `Y1` Consumption plan design.

Why:

- It gives CruSibyl a fixed, always-warm host for timer-trigger reliability.
- It avoids introducing a second paid compute plan just for functions.
- It fits the app's current workload better than a Consumption plan with a 10-minute ceiling.
- It keeps the migration simpler than introducing Flex Consumption or Premium in the first pass.

Operational follow-up to include during migration:

- Add timer diagnostics to `PackageVersionSyncFunction` so logs capture `timer.IsPastDue` and schedule metadata.
- Add explicit post-deploy function host validation so deployments verify host health and registered functions before completion.

## Proposed Bicep Layout

Mirror `../readable` where it helps, but drop modules CruSibyl does not need.

```text
infrastructure/azure/
  README.md
  main.bicep
  modules/
    compute.bicep
    sql.bicep
    function-storage.bicep
    monitoring.bicep
    rbac.bicep
  scripts/
    deploy.sh
    deploy_test.sh
    deploy_prod.sh
  pipelines/templates/
    deploy-stage.yml
```

### Mapping from `readable`

- Keep the overall folder structure, wrapper scripts, and pipeline-template pattern.
- Reuse the separation between `main.bicep` and small modules.
- Reuse the pattern of resolving deployed app names from Bicep outputs in the pipeline.
- Do not copy `servicebus.bicep`, `eventgrid.bicep`, `eventgrid-subscription.bicep`, or related RBAC modules.
- Adapt `compute.bicep` and `sql.bicep` rather than copying them blindly.

## Likely Resource Model

### `main.bicep`

Responsibilities:

- Accept environment, naming, location, tags, SQL admin parameters, and monitoring options
- Create shared resources or compose modules
- Output deployed resource names needed by scripts and pipelines

### `modules/compute.bicep`

Responsibilities:

- Shared App Service plan
- Web App
- Function App
- `Always On` and related site configuration
- Base app settings
- Managed identities

### `modules/sql.bicep`

Responsibilities:

- SQL server
- SQL database
- Minimal firewall rules for v1
- Outputs for server/database names

### `modules/function-storage.bicep`

Responsibilities:

- Storage account for function runtime/content needs
- Container or file-share support if required by the chosen hosting model
- Output connection string and account name

### `modules/monitoring.bicep`

Responsibilities:

- Log Analytics workspace
- Application Insights resource
- Outputs for connection string

### `modules/rbac.bicep`

Responsibilities:

- Resource-group-local role assignments if needed
- Document or implement the subscription-scope role assignment strategy for the function app identity

Note:

- CruSibyl's function app scans subscriptions, so the most important RBAC is likely at the subscription scope, not the resource-group scope.
- We may implement subscription-scope role assignment in Bicep, in deploy scripts, or in a separate step if cross-scope deployment proves awkward.

## Pipeline Direction

### Current state

- Current `azure-pipelines.yml` builds and deploys code only.
- The web app and function app names are hard-coded in pipeline tasks.
- App settings are only partially managed during deploy.

### Target state

- Add an optional infra deployment step modeled after `../readable`.
- Resolve app names from Bicep outputs instead of hard-coding them in the pipeline.
- Use an explicit app-settings step for environment-specific secrets and runtime config.
- Add a post-deploy validation step for the Function App:
  - verify the Functions host is healthy
  - verify expected functions are registered
  - fail the deployment if host validation does not pass
- Keep code deployment separate from infra deployment, but orchestrate both in one pipeline.

### Reliability hardening to include with migration

- Add timer-trigger diagnostics to `PackageVersionSyncFunction`:
  - log `timer.IsPastDue`
  - log available schedule status metadata
  - make it easy to distinguish scheduled runs from catch-up runs in telemetry
- Treat Functions host health as a deployment concern, not only a runtime concern:
  - include host/function registration checks after deployment
  - include a recovery path such as restart/retry if validation fails before the pipeline gives up

## Migration Phases

### Phase 1: Finalize design

- [x] Inventory current test resources
- [x] Inventory code dependencies
- [x] Inspect `readable` implementation
- [x] Decide SQL networking model for v1
- [x] Decide monitoring approach for v1
- [x] Decide secret-management approach for v1
- [x] Decide function hosting plan for v1

### Phase 2: Scaffold infrastructure files

- [x] Create `infrastructure/azure/`
- [x] Add `main.bicep`
- [x] Add initial modules
- [x] Add `README.md`
- [x] Add deploy scripts

### Phase 3: Encode core infrastructure

- [x] Bicep for shared App Service plan
- [x] Bicep for web app on shared plan
- [x] Bicep for function app on shared plan
- [x] Bicep for `Always On` site configuration
- [x] Bicep for function storage
- [x] Bicep for SQL server/database
- [x] Bicep for monitoring
- [x] Bicep or script support for RBAC

### Phase 4: Update delivery workflow

- [x] Update `azure-pipelines.yml`
- [x] Add reusable deploy-stage template
- [x] Move environment-specific settings into variable groups / app-settings task
- [x] Ensure infra outputs drive app deploy steps
- [x] Add post-deploy Functions host health / registration validation
- [ ] Add timer diagnostics to `PackageVersionSyncFunction`

### Phase 5: Test environment rebuild

- [ ] Run `az deployment group what-if`
- [ ] Delete or replace existing test resources as needed
- [ ] Deploy test infra from Bicep
- [ ] Deploy web app code
- [ ] Deploy function app code
- [ ] Run post-deploy Functions host validation
- [ ] Apply database migrations

### Phase 6: Validation

- [ ] Web app starts successfully
- [ ] OIDC login works
- [ ] SQL connectivity works from web app
- [ ] SQL connectivity works from function app
- [ ] Function timers are enabled and healthy
- [ ] `PackageVersionSyncFunction` emits timer diagnostics including `IsPastDue`
- [ ] Function host health validation passes after deploy
- [ ] Manual HTTP function triggers work
- [ ] Function managed identity can enumerate target subscriptions
- [ ] App Insights receives telemetry
- [ ] A full delete/recreate cycle works cleanly

### Phase 7: Production rollout

- [ ] Inventory current production resources
- [ ] Define prod parameters and naming
- [ ] Apply the same Bicep/pipeline model to prod
- [ ] Deploy with approval gates

## Open Questions

- Should v1 preserve the current resource names exactly, or should we switch to deterministic generated names and let the pipeline consume outputs?
- Do we want a dev environment alongside test/prod from the beginning?
- Should the web app also receive a managed identity in v1, even if we only need the function app identity today?
- Do we want to normalize the `Azure__Subscriptions__*` config key naming while we touch deployment settings?
- Do we want SQL Entra/managed-identity auth later, or is SQL auth acceptable for the first Bicep milestone?
- Should the new Bicep standardize on `westus2`, preserve `westus`, or revisit region choice entirely?

## Working Assumptions

Unless we decide otherwise before implementation, this plan assumes:

- Test can be rebuilt from scratch without preserving current resources.
- `westus2` remains the target region.
- We will model the repo layout after `../readable`, but not copy unneeded infrastructure modules.
- v1 will optimize for clarity and repeatability over maximum network isolation.
- The web app and function app will share one App Service plan, with `Always On` enabled.
