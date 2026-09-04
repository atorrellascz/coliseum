#!/usr/bin/env bash
# Build the local images, install (or upgrade) the chart on a local cluster and run the smoke test.
#   KUBE_CONTEXT=docker-desktop bash scripts/helm-up.sh
#   KUBE_CONTEXT=k3d-coliseum  bash scripts/helm-up.sh      (after scripts/k3d-up.sh)
# Always passes --context explicitly: never rely on the globally selected kubectl context.
set -euo pipefail
cd "$(dirname "$0")/.."

CONTEXT="${KUBE_CONTEXT:-docker-desktop}"
NAMESPACE="${NAMESPACE:-coliseum}"
RELEASE="${RELEASE:-coliseum}"
K="kubectl --context $CONTEXT"

echo "== building images"
docker build --target api    -t coliseum-api:local    . >/dev/null
docker build --target worker -t coliseum-worker:local . >/dev/null
docker build --target mcp    -t coliseum-mcp:local    . >/dev/null

if [[ "$CONTEXT" == k3d-* ]]; then
  echo "== importing images into $CONTEXT"
  k3d image import -c "${CONTEXT#k3d-}" coliseum-api:local coliseum-worker:local coliseum-mcp:local
fi

echo "== helm upgrade --install ($CONTEXT / $NAMESPACE)"
START=$(date +%s)
helm --kube-context "$CONTEXT" upgrade --install "$RELEASE" deploy/helm/coliseum \
  --namespace "$NAMESPACE" --create-namespace \
  -f deploy/helm/coliseum/values-local.yaml \
  --wait --timeout 180s
echo "   helm install took $(( $(date +%s) - START ))s"

$K -n "$NAMESPACE" get pods

echo "== port-forward + smoke"
$K -n "$NAMESPACE" port-forward "svc/$RELEASE-coliseum-api" 18080:80 >/dev/null 2>&1 &
PF=$!
trap 'kill $PF 2>/dev/null || true' EXIT
for i in $(seq 1 30); do curl -sf http://localhost:18080/healthz/ready >/dev/null && break; sleep 1; done
API_URL=http://localhost:18080 API_KEY=dev-service-key bash scripts/smoke.sh
