#!/usr/bin/env bash
set -euo pipefail

if ! command -v az >/dev/null 2>&1; then
  echo "Azure CLI (az) is required but was not found on PATH." >&2
  exit 1
fi

if ! az account show >/dev/null 2>&1; then
  echo "You must run 'az login' before deploying." >&2
  exit 1
fi

SCRIPT_DIR=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)
BICEP_FILE="$SCRIPT_DIR/../main.bicep"

sanitize_pipeline_value() {
  local value="${1:-}"

  # Azure DevOps leaves unresolved macro references in the form $(VAR_NAME).
  # Treat those as unset so optional overrides do not get forwarded into Bicep.
  if [[ "$value" =~ ^\$\([A-Za-z0-9_.-]+\)$ ]]; then
    printf ''
    return
  fi

  printf '%s' "$value"
}

normalize_subscription_ids_parameter() {
  local value="${1:-}"
  value="${value#"${value%%[![:space:]]*}"}"
  value="${value%"${value##*[![:space:]]}"}"

  if [[ -z "$value" ]]; then
    printf ''
    return
  fi

  if [[ "$value" == \[* ]]; then
    printf '%s' "$value"
    return
  fi

  local result="["
  local first=1
  local id

  IFS=',' read -ra ids <<< "$value"
  for id in "${ids[@]}"; do
    id="${id#"${id%%[![:space:]]*}"}"
    id="${id%"${id##*[![:space:]]}"}"

    if [[ -z "$id" ]]; then
      continue
    fi

    if [[ ! "$id" =~ ^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$ ]]; then
      echo "AZURE_SYNC_SUBSCRIPTION_IDS must contain subscription GUIDs. Invalid value: $id" >&2
      exit 1
    fi

    if (( first )); then
      first=0
    else
      result+=", "
    fi

    result+="\"$id\""
  done

  result+="]"
  printf '%s' "$result"
}

DEPLOYMENT_NAME=$(sanitize_pipeline_value "${DEPLOYMENT_NAME:-}")
RESOURCE_GROUP=$(sanitize_pipeline_value "${RESOURCE_GROUP:-}")
LOCATION=$(sanitize_pipeline_value "${LOCATION:-}")
APP_NAME=$(sanitize_pipeline_value "${APP_NAME:-}")
ENVIRONMENT=$(sanitize_pipeline_value "${ENVIRONMENT:-}")
SQL_ADMIN_LOGIN=$(sanitize_pipeline_value "${SQL_ADMIN_LOGIN:-}")
SQL_ADMIN_PASSWORD=$(sanitize_pipeline_value "${SQL_ADMIN_PASSWORD:-}")

DEPLOY_MONITORING=$(sanitize_pipeline_value "${DEPLOY_MONITORING:-}")
DEPLOY_MONITORING=${DEPLOY_MONITORING:-true}

APP_SERVICE_PLAN_SKU=$(sanitize_pipeline_value "${APP_SERVICE_PLAN_SKU:-}")
APP_SERVICE_PLAN_SKU=${APP_SERVICE_PLAN_SKU:-B1}

APP_SERVICE_PLAN_TIER=$(sanitize_pipeline_value "${APP_SERVICE_PLAN_TIER:-}")
APP_SERVICE_PLAN_TIER=${APP_SERVICE_PLAN_TIER:-Basic}

APP_SERVICE_PLAN_CAPACITY=$(sanitize_pipeline_value "${APP_SERVICE_PLAN_CAPACITY:-}")
APP_SERVICE_PLAN_CAPACITY=${APP_SERVICE_PLAN_CAPACITY:-1}

SQL_DATABASE_NAME=$(sanitize_pipeline_value "${SQL_DATABASE_NAME:-}")
SQL_DATABASE_NAME=${SQL_DATABASE_NAME:-${APP_NAME:-crusibyl}}

APP_SERVICE_PLAN_NAME=$(sanitize_pipeline_value "${APP_SERVICE_PLAN_NAME:-}")
WEB_APP_NAME=$(sanitize_pipeline_value "${WEB_APP_NAME:-}")
FUNCTION_APP_NAME=$(sanitize_pipeline_value "${FUNCTION_APP_NAME:-}")
FUNCTION_STORAGE_ACCOUNT_NAME=$(sanitize_pipeline_value "${FUNCTION_STORAGE_ACCOUNT_NAME:-}")
SQL_SERVER_NAME=$(sanitize_pipeline_value "${SQL_SERVER_NAME:-}")
APP_INSIGHTS_NAME=$(sanitize_pipeline_value "${APP_INSIGHTS_NAME:-}")
LOG_ANALYTICS_WORKSPACE_NAME=$(sanitize_pipeline_value "${LOG_ANALYTICS_WORKSPACE_NAME:-}")
AZURE_SYNC_SUBSCRIPTION_IDS=$(sanitize_pipeline_value "${AZURE_SYNC_SUBSCRIPTION_IDS:-}")
AZURE_SYNC_SUBSCRIPTION_ROLE=$(sanitize_pipeline_value "${AZURE_SYNC_SUBSCRIPTION_ROLE:-}")

SUBSCRIPTION_ID=$(sanitize_pipeline_value "${SUBSCRIPTION_ID:-}")
SUBSCRIPTION_NAME=$(sanitize_pipeline_value "${SUBSCRIPTION_NAME:-}")

required_vars=(
  DEPLOYMENT_NAME
  RESOURCE_GROUP
  LOCATION
  APP_NAME
  ENVIRONMENT
  SQL_ADMIN_LOGIN
  SQL_ADMIN_PASSWORD
)

missing_vars=()
for var in "${required_vars[@]}"; do
  if [[ -z "${!var:-}" ]]; then
    missing_vars+=("$var")
  fi
done

if (( ${#missing_vars[@]} > 0 )); then
  echo "Missing required environment variables (unset or unresolved pipeline placeholders): ${missing_vars[*]}" >&2
  exit 1
fi

subscription_value=""
subscription_label=""

if [[ -n "${SUBSCRIPTION_ID:-}" ]]; then
  subscription_value="$SUBSCRIPTION_ID"
  subscription_label=${SUBSCRIPTION_NAME:-}
elif [[ -n "${SUBSCRIPTION_NAME:-}" ]]; then
  subscription_value="$SUBSCRIPTION_NAME"
fi

if [[ -n "$subscription_value" ]]; then
  if [[ -n "$subscription_label" ]]; then
    echo "Setting Azure subscription to $subscription_label ($subscription_value)..."
  else
    echo "Setting Azure subscription to $subscription_value..."
  fi
  az account set --subscription "$subscription_value"
fi

echo "Ensuring resource group $RESOURCE_GROUP exists in $LOCATION..."
az group create --name "$RESOURCE_GROUP" --location "$LOCATION" --output none

parameter_args=(
  --parameters "appName=$APP_NAME"
  --parameters "env=$ENVIRONMENT"
  --parameters "location=$LOCATION"
  --parameters "sqlAdminLogin=$SQL_ADMIN_LOGIN"
  --parameters "sqlAdminPassword=$SQL_ADMIN_PASSWORD"
  --parameters "sqlDatabaseName=$SQL_DATABASE_NAME"
  --parameters "deployMonitoring=$DEPLOY_MONITORING"
  --parameters "planSkuName=$APP_SERVICE_PLAN_SKU"
  --parameters "planSkuTier=$APP_SERVICE_PLAN_TIER"
  --parameters "planCapacity=$APP_SERVICE_PLAN_CAPACITY"
)

if [[ -n "${APP_SERVICE_PLAN_NAME:-}" ]]; then
  parameter_args+=(--parameters "appServicePlanName=$APP_SERVICE_PLAN_NAME")
fi

if [[ -n "${WEB_APP_NAME:-}" ]]; then
  parameter_args+=(--parameters "webAppName=$WEB_APP_NAME")
fi

if [[ -n "${FUNCTION_APP_NAME:-}" ]]; then
  parameter_args+=(--parameters "functionAppName=$FUNCTION_APP_NAME")
fi

if [[ -n "${FUNCTION_STORAGE_ACCOUNT_NAME:-}" ]]; then
  parameter_args+=(--parameters "functionStorageAccountName=$FUNCTION_STORAGE_ACCOUNT_NAME")
fi

if [[ -n "${SQL_SERVER_NAME:-}" ]]; then
  parameter_args+=(--parameters "sqlServerName=$SQL_SERVER_NAME")
fi

if [[ -n "${APP_INSIGHTS_NAME:-}" ]]; then
  parameter_args+=(--parameters "appInsightsName=$APP_INSIGHTS_NAME")
fi

if [[ -n "${LOG_ANALYTICS_WORKSPACE_NAME:-}" ]]; then
  parameter_args+=(--parameters "logAnalyticsWorkspaceName=$LOG_ANALYTICS_WORKSPACE_NAME")
fi

if [[ -n "${AZURE_SYNC_SUBSCRIPTION_IDS:-}" ]]; then
  normalized_sync_subscription_ids=$(normalize_subscription_ids_parameter "$AZURE_SYNC_SUBSCRIPTION_IDS")
  parameter_args+=(--parameters "azureSyncSubscriptionIds=$normalized_sync_subscription_ids")
fi

if [[ -n "${AZURE_SYNC_SUBSCRIPTION_ROLE:-}" ]]; then
  parameter_args+=(--parameters "azureSyncSubscriptionRole=$AZURE_SYNC_SUBSCRIPTION_ROLE")
fi

echo "Deploying infrastructure from $BICEP_FILE..."
az deployment group create \
  --resource-group "$RESOURCE_GROUP" \
  --name "$DEPLOYMENT_NAME" \
  --template-file "$BICEP_FILE" \
  "${parameter_args[@]}"
