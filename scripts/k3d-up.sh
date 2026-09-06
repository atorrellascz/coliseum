#!/usr/bin/env bash
# Create a local multi-node k3d cluster and install Coliseum on it (requires k3d: https://k3d.io).
#   bash scripts/k3d-up.sh          # create cluster "coliseum" (1 server, 2 agents) and deploy
#   k3d cluster delete coliseum     # tear down
set -euo pipefail
cd "$(dirname "$0")/.."

CLUSTER="${CLUSTER:-coliseum}"
# k3d has no winget package: it lives in ~/bin on Windows (Git Bash puts ~/bin on PATH, PowerShell does not).
export PATH="$HOME/bin:$PATH"
command -v k3d >/dev/null || { echo "k3d is not installed: download k3d.exe from https://github.com/k3d-io/k3d/releases into ~/bin (or choco install k3d / brew install k3d). From PowerShell use scripts\run.ps1: a plain bash may be WSL."; exit 1; }

if ! k3d cluster list | grep -q "^$CLUSTER "; then
  START=$(date +%s)
  k3d cluster create "$CLUSTER" --agents 2 --wait
  echo "cluster created in $(( $(date +%s) - START ))s"
fi

KUBE_CONTEXT="k3d-$CLUSTER" bash scripts/helm-up.sh
