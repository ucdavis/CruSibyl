#!/usr/bin/env bash
set -euo pipefail
SCRIPT_DIR=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)
export DEPLOY_ENV=test
bash "$SCRIPT_DIR/deploy.sh"
