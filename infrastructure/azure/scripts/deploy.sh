#!/usr/bin/env bash
set -euo pipefail
SCRIPT_DIR=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)
# Infrastructure and runtime settings only. The OIDC bootstrap creates the group.
# Inputs and validation are shared with Configure Azure through the Node helper.
node "$SCRIPT_DIR/../../../scripts/azure-deployment.mts" configure
