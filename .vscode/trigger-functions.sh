#!/bin/bash
# Trigger Azure Functions locally for testing
# Usage: ./trigger-functions.sh [function-name]
# Available functions: manifest, webjob, packageversion, webjobtatus, all

BASE_URL="http://localhost:7071/api"

trigger_manifest() {
    echo "🔄 Triggering ManifestSyncFunction..."
    curl -X POST "${BASE_URL}/ManifestSyncFunction_Http" \
        -H "Content-Type: application/json" \
        -w "\n✓ Status: %{http_code}\n"
}

trigger_webjob() {
    echo "🔄 Triggering AppWebJobSyncFunction..."
    curl -X POST "${BASE_URL}/AppWebJobSyncFunction_Http" \
        -H "Content-Type: application/json" \
        -w "\n✓ Status: %{http_code}\n"
}

trigger_packageversion() {
    echo "🔄 Triggering PackageVersionSyncFunction..."
    curl -X POST "${BASE_URL}/PackageVersionSyncFunction_Http" \
        -H "Content-Type: application/json" \
        -w "\n✓ Status: %{http_code}\n"
}

trigger_webjobstatus() {
    echo "🔄 Triggering WebJobStatusSyncFunction..."
    curl -X POST "${BASE_URL}/WebJobStatusSyncFunction_Http" \
        -H "Content-Type: application/json" \
        -w "\n✓ Status: %{http_code}\n"
}

trigger_all() {
    echo "🚀 Triggering all functions..."
    echo ""
    trigger_manifest
    echo ""
    trigger_webjob
    echo ""
    trigger_packageversion
    echo ""
    trigger_webjobstatus
}

case "$1" in
    manifest)
        trigger_manifest
        ;;
    webjob)
        trigger_webjob
        ;;
    packageversion)
        trigger_packageversion
        ;;
    webjobstatus)
        trigger_webjobstatus
        ;;
    all)
        trigger_all
        ;;
    *)
        echo "Usage: $0 {manifest|webjob|packageversion|webjobstatus|all}"
        echo ""
        echo "Available functions:"
        echo "  manifest        - Sync app manifests from Azure"
        echo "  webjob          - Sync WebJobs from Azure App Services"
        echo "  packageversion  - Sync package versions (NuGet/npm)"
        echo "  webjobstatus    - Sync WebJob status updates and logs"
        echo "  all             - Trigger all functions sequentially"
        exit 1
        ;;
esac
