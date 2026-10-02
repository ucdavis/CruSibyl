# CruSibyl deployment

GitHub Actions is the deployment path. CruSibyl is an MVC/HTMX application plus a .NET isolated Function App; neither deployment uses a React build.

## Defaults and boundaries

| Setting | Test | Production |
| --- | --- | --- |
| Resource group | `rg-crusibyl-test` | `rg-crusibyl-prod` |
| Existing Linux plan | `DefaultPlan2` | `Nibbler` |
| Plan resource group | `Default-Web-WestUS` | `service-plans-linux` |
| SQL SKU / tier | Basic / Basic | S0 / Standard |
| SQL Azure-services firewall exception | enabled | disabled |
| .NET runtime environment | Staging | Production |

Web, Function, SQL, and monitoring names include the application, environment, and a deterministic resource-group-derived token. Both applications use the existing plan's region and Always On. SQL/storage credentials and connection strings remain infrastructure-owned. Both apps read `ConnectionStrings:DefaultConnection` from App Service's SQLAzure connection-string entry.

Scripts verify the signed-in subscription against `AZURE_SUBSCRIPTION_ID`; they never silently switch subscriptions. Resource-group names must end in `-test` or `-prod`. Bicep entrypoints also guard these boundaries. Check `deploymentGuardPassed` in bootstrap outputs.

This setup does not rename, delete, transfer, or migrate existing Azure resources or data. Choosing and executing an existing-resource cutover is separate work.

## Schema prerequisite

**The runtime model no longer contains `User.Kerberos`. The user must generate the SQL Server and SQLite migrations. Historical migrations and snapshots are intentionally unchanged.** Complete that step before releasing the package or attempting migration-backed application startup. The web app still applies migrations at startup; this refactor does not bypass pending model-change validation. Keep the web deployment before the Function deployment so database initialization can finish first.

## One-time setup for each environment

Use an operator identity with resource creation and RBAC-assignment privileges. GitHub's deployment identity receives Contributor on the application group and Website Contributor on the exact shared plan; it does not receive role-assignment privileges.

The bootstrap creates the application resource group, a user-assigned managed identity, and its GitHub Actions federated credential. These deployment credentials are distinct from the Entra app registration used by people to sign in; keep `AUTH_CLIENT_ID` and `Auth:ClientId` configured for user sign-in. Configure Azure expects the application resource group to exist.

### Select the environment

Run these examples in Bash from the repository root, using authenticated Azure CLI and GitHub CLI sessions. Replace the subscription placeholder with the intended subscription ID. Select the **test** values first:

```bash
env="test"
subscription_id="<test-subscription-id>"
resource_group="rg-crusibyl-test"
web_plan_name="DefaultPlan2"
web_plan_resource_group="Default-Web-WestUS"
```

After completing test setup and the deployment verification below, repeat the shared steps with the **production** values:

```bash
env="prod"
subscription_id="<prod-subscription-id>"
resource_group="rg-crusibyl-prod"
web_plan_name="Nibbler"
web_plan_resource_group="service-plans-linux"
```

If overriding the shared plan, change both `web_plan_name` and `web_plan_resource_group` before running the bootstrap. The GitHub configuration commands below use the same values so deployment permissions and infrastructure target the same plan.

### Validate, preview, and apply

For the selected environment, prepare the common arguments. Each command explicitly targets the selected subscription:

```bash
deployment_name="crusibyl-${env}-oidc"
bootstrap_args=(
  --subscription "$subscription_id"
  --location westus2
  --template-file infrastructure/azure/github-oidc.bicep
  --parameters
  "appName=crusibyl"
  "repository=ucdavis/CruSibyl"
  "env=$env"
  "expectedSubscriptionId=$subscription_id"
  "resourceGroupName=$resource_group"
  "location=westus2"
  "webPlanName=$web_plan_name"
  "webPlanResourceGroup=$web_plan_resource_group"
)
az deployment sub validate "${bootstrap_args[@]}"
az deployment sub what-if "${bootstrap_args[@]}"
```

Proceed only after validation succeeds and you have reviewed the preview:

```bash
az deployment sub create \
  --name "$deployment_name" \
  "${bootstrap_args[@]}" \
  --query properties.outputs
```

Before configuring GitHub, verify these outputs:

- `deploymentGuardPassed` is `true`.
- `deploymentIdentityName`, `clientId`, `principalId`, `tenantId`, `subscriptionId`, and `resourceGroupName` are populated. Confirm the tenant, subscription, and resource group match the intended environment. Default identity names are `id-crusibyl-test-deploy` and `id-crusibyl-prod-deploy`.
- `federatedCredentialSubject` is exactly `repo:ucdavis/CruSibyl:environment:test` or `repo:ucdavis/CruSibyl:environment:prod`, matching the selected environment.
- `roleAssignmentId` and `webPlanRoleAssignmentId` are populated with the default `assignRbac=true`.

A false guard creates no resources and emits empty identity outputs; a successful CLI exit alone is insufficient. Check the subscription, repository, and matching `-test` or `-prod` resource-group suffix before proceeding. Reapply bootstrap after repository, environment, resource-group, subscription, identity, or plan changes, including deletion of the managed identity.

### Configure GitHub Environments

You need repository admin permission and a GitHub CLI token that can manage repository environments and variables, plus secrets for the subsequent configuration step. Create GitHub Environments named `test` and `prod`:

```bash
gh api --method PUT repos/ucdavis/CruSibyl/environments/test
gh api --method PUT repos/ucdavis/CruSibyl/environments/prod
```

Configure production's deployment branch restrictions and required reviewers as appropriate. After verifying the bootstrap outputs, populate the selected environment's variables:

| GitHub variable | Bootstrap output |
| --- | --- |
| `AZURE_CLIENT_ID` | `clientId` |
| `AZURE_TENANT_ID` | `tenantId` |
| `AZURE_SUBSCRIPTION_ID` | `subscriptionId` |
| `RESOURCE_GROUP` | `resourceGroupName` |

```bash
gh variable set AZURE_CLIENT_ID --repo ucdavis/CruSibyl --env "$env" --body "$(az deployment sub show --subscription "$subscription_id" --name "$deployment_name" --query properties.outputs.clientId.value --output tsv)"
gh variable set AZURE_TENANT_ID --repo ucdavis/CruSibyl --env "$env" --body "$(az deployment sub show --subscription "$subscription_id" --name "$deployment_name" --query properties.outputs.tenantId.value --output tsv)"
gh variable set AZURE_SUBSCRIPTION_ID --repo ucdavis/CruSibyl --env "$env" --body "$(az deployment sub show --subscription "$subscription_id" --name "$deployment_name" --query properties.outputs.subscriptionId.value --output tsv)"
gh variable set RESOURCE_GROUP --repo ucdavis/CruSibyl --env "$env" --body "$(az deployment sub show --subscription "$subscription_id" --name "$deployment_name" --query properties.outputs.resourceGroupName.value --output tsv)"
gh variable set AZURE_LOCATION --repo ucdavis/CruSibyl --env "$env" --body westus2
gh variable set WEB_PLAN_NAME --repo ucdavis/CruSibyl --env "$env" --body "$web_plan_name"
gh variable set WEB_PLAN_RESOURCE_GROUP --repo ucdavis/CruSibyl --env "$env" --body "$web_plan_resource_group"
```

If an environment or variable command returns `HTTP 404: Not Found`, confirm the environment exists in `ucdavis/CruSibyl`, the selected `env` is correct, and the authenticated GitHub CLI account/token has the required repository permissions.

Next, provide the runtime settings and `SQL_ADMIN_PASSWORD` described below and run **Configure Azure** for the selected environment. Complete the separate Function runtime RBAC step after provisioning, then follow [Package deployment and verification](#package-deployment-and-verification). Finish test verification before repeating the setup for production.

## Configure infrastructure and settings

Run **Configure Azure** for the selected environment. Its `deploy_infra` input defaults to true; use false for runtime settings only. It requires the runtime settings catalog values described below and, for infrastructure deployment, the `SQL_ADMIN_PASSWORD` environment secret.

Optional infrastructure variables: `APP_NAME` (default `crusibyl`), `DEPLOYMENT_NAME`, `AZURE_LOCATION`, `SQL_ADMIN_LOGIN` (default `sqladmin`), `SQL_DATABASE_NAME`, paired `SQL_SKU_NAME`/`SQL_SKU_TIER`, and paired `WEB_PLAN_NAME`/`WEB_PLAN_RESOURCE_GROUP`. The workflow's shared concurrency group serializes it with package deployments for the same environment.

Configure Azure snapshots existing non-platform app settings before infrastructure deployment and reapplies them alongside platform settings. Runtime updates merge catalog-managed keys. Connection strings and infrastructure-derived values cannot be overridden through the catalog. Settings values use temporary permission-restricted JSON files; CLI output that could contain credentials is suppressed, and temporary files are removed after use.

After first provisioning, use the operator identity to grant the Function identity its runtime access:

```bash
az deployment sub create \
  --name crusibyl-test-runtime-rbac --location westus2 \
  --template-file infrastructure/azure/runtime-rbac.bicep \
  --parameters env=test expectedSubscriptionId='<subscription-id>' \
    resourceGroupName=rg-crusibyl-test functionAppName='<functionAppName output>'
```

The runtime entrypoint resolves the deployed Function's system-assigned identity and assigns Website Contributor in that subscription for Azure/Kudu scanning. Reapply it if the Function identity is recreated. Normal Configure Azure/package workflows do not run this step.

### Production SQL connectivity

Before production package deployment, an operator must configure SQL firewall rules covering the web and Function apps' outbound addresses, including addresses that can be used after plan changes. Both applications need access. Review these rules when hosting changes. The template does not add private endpoints, VNet integration, or broad production Azure-services access.

## Runtime settings catalog

Edit `deployment-settings.json` for application additions, overrides, or disabled built-ins. `deployment-settings-defaults.json` contains the built-in catalog, and the schema provides editor support.

Each setting declares its GitHub name, App Service name, `targets` (`web`, `functions`, or both), variable/secret classification, primitive type, description, required condition, optional default, and empty-value behavior. `requiredWhen` supports `always`, `deploy_infra`, `existing_infra`, and `never`; `emitWhen: always` explicitly writes an empty string when optional input is empty. Otherwise empty values are omitted. Disabling an entry stops management; it does not delete deployed values.

```bash
npm ci
npm run deployment-settings:sync
npm run deployment-settings:check
npm test
```

Sync regenerates Configure Azure's environment bindings and `deployment-settings.generated.json`. Local deployment consumes the same validated defaults/overlay resolver. `--check` rejects stale generated outputs. The generated JSON contains metadata/defaults, never supplied secret values.

Required runtime inputs:

| GitHub name | Classification | Destination |
| --- | --- | --- |
| `AUTH_CLIENT_ID` | variable | Web `Auth__ClientId` |
| `AUTH_CLIENT_SECRET` | secret | Web `Auth__ClientSecret` |
| `IAM_KEY` | secret | Web `Auth__IamKey` |
| `REPO_READ_TOKEN` | secret | Functions `GitHub__AccessToken` |

Built-in defaults cover `AUTH_INSTANCE`, `AUTH_TENANT_ID`, `AUTH_DOMAIN`, `AUTH_CALLBACK_PATH`, `REPO_OWNER`, application log names, sync concurrency, and dashboard windows. `SERILOG_ELASTIC_URL` is an optional secret for both applications; empty explicitly disables the sink. See the catalog for every mapping. Infrastructure credentials, connection strings, Function storage, subscription, environment, and monitoring settings are not runtime overlay entries.

## Entra sign-in and IAM

Configure a single-tenant UC Davis Entra web app registration with the callback `/signin-oidc` on each actual application hostname and local development origin. The registration must emit `ucdPersonIAMID` in the sign-in token, as the working web-app-template registration does. Configure its client ID/credential through the web runtime settings above; do not use the deployment identity's client ID.

Locally configure `Auth:ClientId`, `Auth:ClientSecret`, and `Auth:IamKey` using .NET user secrets. Existing `Authentication:*` CAS settings are no longer read.

The application requires one nonempty numeric IAM claim. Existing users are found strictly by IAM and retain their IDs and permissions. New users are resolved through IAM people/contact endpoints and receive no automatic permissions. Email/IAM administrative lookup never merges different IAM identities; ambiguous directory results fail. Neither sign-in nor administration depends on campus usernames or Kerberos endpoints. Entra token role claims are not application permissions.

The HTMX popup routes and auth headers remain unchanged. Cookie names differ from CAS, requiring a fresh sign-in. App Service requests trust the immediate platform ingress for forwarded protocol/client address; other hosts retain the framework's loopback proxy defaults. Additional reverse proxies need an explicit trust configuration. Data Protection keys live under persistent App Service `HOME/ASP.NET/DataProtection-Keys/<environment>` (or local application data during development), separate from deployed packages and isolated by application/environment. Slots do not share cookies automatically.

## Package deployment and verification

PRs build/test without Azure access. Main pushes deploy to test after validation. Manual **CI/CD** runs select test or prod and deploy both artifacts from the same commit. Package deployment receives OIDC/environment coordinates but no application runtime secrets. Resolve apps through exactly one matching application/environment tag and web/function resource kind; ambiguity fails instead of guessing.

Deployment checks `/health` for HTTP 200 and verifies the Function App is Running with all four timer functions and their HTTP companions registered. `APP_HEALTH_TIMEOUT_SECONDS` defaults to 120 and must be positive. These checks are bounded; they do not restart the apps or invoke synchronization functions as a side effect.

After a separately authorized deployment:

1. Confirm `/health` is healthy and SQL access works.
2. Verify Entra sign-in, an existing administrator's permissions, HTMX popup reauthentication, and forbidden responses.
3. Verify Function logs and actual synchronization; registrations alone do not prove successful scans or runtime RBAC.

Local infrastructure/configuration equivalents:

```bash
export AZURE_SUBSCRIPTION_ID='<subscription-id>'
# Export required secrets and runtime settings securely before running.
bash infrastructure/azure/scripts/deploy_test.sh
# Or deploy_prod.sh with the production subscription selected.
```

Set `DEPLOY_INFRA=false` for settings-only work. These scripts do not deploy code. Use `node scripts/azure-deployment.mts verify` with `DEPLOY_ENV` and subscription inputs to run the same read-only readiness checks after a deployment.
