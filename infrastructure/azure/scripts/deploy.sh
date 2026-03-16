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

DEPLOY_MONITORING=${DEPLOY_MONITORING:-true}
APP_SERVICE_PLAN_SKU=${APP_SERVICE_PLAN_SKU:-B1}
APP_SERVICE_PLAN_TIER=${APP_SERVICE_PLAN_TIER:-Basic}
APP_SERVICE_PLAN_CAPACITY=${APP_SERVICE_PLAN_CAPACITY:-1}
SQL_DATABASE_NAME=${SQL_DATABASE_NAME:-${APP_NAME:-crusibyl}}

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
  echo "Missing required environment variables: ${missing_vars[*]}" >&2
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

echo "Deploying infrastructure from $BICEP_FILE..."
az deployment group create \
  --resource-group "$RESOURCE_GROUP" \
  --name "$DEPLOYMENT_NAME" \
  --template-file "$BICEP_FILE" \
  "${parameter_args[@]}"
