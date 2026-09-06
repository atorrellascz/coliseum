#!/usr/bin/env bash
# Install Argo CD on a local cluster and let it sync Coliseum from GitHub (GitOps demo).
#   KUBE_CONTEXT=k3d-coliseum bash scripts/argocd-up.sh        # after scripts/k3d-up.sh (images imported)
#   KUBE_CONTEXT=docker-desktop bash scripts/argocd-up.sh
# Prints the UI URL and the admin password; leaves port-forwards running (Argo UI 8443; API 8080, MCP 8082,
# Grafana 3000 re-established, since uninstalling the helm release drops the ones from helm-up.sh).
set -euo pipefail
cd "$(dirname "$0")/.."
CONTEXT="${KUBE_CONTEXT:-k3d-coliseum}"
K="kubectl --context $CONTEXT"
ARGO_VERSION="${ARGO_VERSION:-stable}"

echo "== Argo CD ($ARGO_VERSION) on $CONTEXT"
$K create namespace argocd --dry-run=client -o yaml | $K apply -f - >/dev/null
# Server-side apply: the ApplicationSet CRD exceeds the 256 KB last-applied annotation limit of client-side apply.
$K apply --server-side --force-conflicts -n argocd -f "https://raw.githubusercontent.com/argoproj/argo-cd/$ARGO_VERSION/manifests/install.yaml" >/dev/null
$K -n argocd rollout status deploy/argocd-server --timeout=240s
$K -n argocd rollout status deploy/argocd-repo-server --timeout=240s

echo "== Coliseum AppProject + Application (source: GitHub main, values-local)"
# The previous helm release must be gone: Argo becomes the only owner of the namespace.
helm --kube-context "$CONTEXT" uninstall coliseum -n coliseum >/dev/null 2>&1 || true
$K apply -f deploy/argocd/project.yaml
$K apply -f deploy/argocd/application.yaml

echo "== waiting for sync (images must already be in the cluster: scripts/k3d-up.sh imports them)"
for i in $(seq 1 60); do
  S=$($K -n argocd get application coliseum -o jsonpath='{.status.sync.status}/{.status.health.status}' 2>/dev/null || true)
  printf '   t+%03ds %s\n' $((i * 5)) "$S"
  [ "$S" = "Synced/Healthy" ] && break
  sleep 5
done
$K -n coliseum get pods

PASS=$($K -n argocd get secret argocd-initial-admin-secret -o jsonpath='{.data.password}' | base64 -d)
$K -n argocd port-forward svc/argocd-server 8443:443 >/dev/null 2>&1 &
# The Services were re-created by Argo: the port-forwards opened by helm-up.sh are dead. Open them again.
KUBE_CONTEXT="$CONTEXT" bash scripts/port-forward.sh
echo
echo "Argo CD UI: https://localhost:8443  (user admin, password: $PASS)  -> application 'coliseum' shows the resource tree"
