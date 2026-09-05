#!/usr/bin/env bash
# Build the local images, install (or upgrade) the chart on a local cluster, port-forward and run the smoke test.
#   KUBE_CONTEXT=docker-desktop bash scripts/helm-up.sh
#   KUBE_CONTEXT=k3d-coliseum  bash scripts/helm-up.sh      (after scripts/k3d-up.sh)
# Always passes --context explicitly: never rely on the globally selected kubectl context.
# The port-forwards (API 18080, Grafana 13000) stay up until this shell exits or `pkill -f port-forward`.
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
  # The bundled Grafana stack is ~1 GB; pulling it inside each k3d node takes minutes, importing it takes seconds.
  docker image inspect grafana/otel-lgtm:latest >/dev/null 2>&1 || docker pull grafana/otel-lgtm:latest >/dev/null
  k3d image import -c "${CONTEXT#k3d-}" grafana/otel-lgtm:latest redis:7-alpine
fi

echo "== helm upgrade --install ($CONTEXT / $NAMESPACE)"
START=$(date +%s)
helm --kube-context "$CONTEXT" upgrade --install "$RELEASE" deploy/helm/coliseum \
  --namespace "$NAMESPACE" --create-namespace \
  -f deploy/helm/coliseum/values-local.yaml \
  --wait --timeout 240s
echo "   helm install took $(( $(date +%s) - START ))s"

$K -n "$NAMESPACE" get pods

echo "== port-forward (API 18080, Grafana 13000) + smoke"
$K -n "$NAMESPACE" port-forward "svc/$RELEASE-coliseum-api" 18080:80 >/dev/null 2>&1 &
$K -n "$NAMESPACE" port-forward "svc/$RELEASE-coliseum-otel-lgtm" 13000:3000 >/dev/null 2>&1 &
for i in $(seq 1 30); do curl -sf http://localhost:18080/healthz/ready >/dev/null && break; sleep 1; done
API_URL=http://localhost:18080 API_KEY=dev-service-key bash scripts/smoke.sh

echo
echo "API:      http://localhost:18080   (arena: /arena/?name=Ata&auto=1  back-office: /backoffice/  docs: /scalar)"
echo "Grafana:  http://localhost:13000   (admin / admin, dashboard Coliseum)"
echo "MCP:      kubectl --context $CONTEXT -n $NAMESPACE port-forward svc/$RELEASE-coliseum-mcp 18082:80"
echo "Port-forwards keep running in the background of this shell; stop them with: pkill -f port-forward"
