#!/usr/bin/env bash
# Create a local multi-node k3d cluster and install Coliseum on it (requires k3d: https://k3d.io).
#   bash scripts/k3d-up.sh          # create cluster "coliseum" (1 server, 2 agents) and deploy
#   k3d cluster delete coliseum     # tear down
set -euo pipefail
cd "$(dirname "$0")/.."

CLUSTER="${CLUSTER:-coliseum}"
command -v k3d >/dev/null || { echo "k3d is not installed (winget install k3d-io.k3d / brew install k3d)"; exit 1; }

if ! k3d cluster list | grep -q "^$CLUSTER "; then
  START=$(date +%s)
  k3d cluster create "$CLUSTER" --agents 2 --wait
  echo "cluster created in $(( $(date +%s) - START ))s"
fi

KUBE_CONTEXT="k3d-$CLUSTER" bash scripts/helm-up.sh
