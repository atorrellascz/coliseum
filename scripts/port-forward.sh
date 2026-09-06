#!/usr/bin/env bash
# Port-forward the Kubernetes services to the SAME local ports the Compose stack uses, so every URL in the docs
# (arena, back-office, Scalar, MCP, Grafana) is identical whichever way the stack was started.
# Consequence: Compose and Kubernetes cannot run at the same time. Run one or the other.
#   KUBE_CONTEXT=k3d-coliseum bash scripts/port-forward.sh
# Forwards API 8080, MCP 8082, Grafana 3000; replaces any previous kubectl port-forward. Stays up until this shell
# exits or `pkill -f port-forward`.
set -euo pipefail
cd "$(dirname "$0")/.."
CONTEXT="${KUBE_CONTEXT:-k3d-coliseum}"
NAMESPACE="${NAMESPACE:-coliseum}"
RELEASE="${RELEASE:-coliseum}"
K="kubectl --context $CONTEXT -n $NAMESPACE"

pkill -f "kubectl.*port-forward" >/dev/null 2>&1 || true
sleep 1

busy() { (exec 3<>"/dev/tcp/127.0.0.1/$1") 2>/dev/null; }
for port in 8080 8082 3000; do
  if busy "$port"; then
    echo "port $port is already in use." >&2
    if docker compose -f deploy/compose/docker-compose.yml ps --services --filter status=running 2>/dev/null | grep -q .; then
      echo "The Compose stack is running. Compose and Kubernetes share the ports 8080-8082 and 3000: run one or the other." >&2
      echo "  docker compose -f deploy/compose/docker-compose.yml down -v" >&2
    else
      echo "  netstat -ano | findstr :$port   ->   taskkill /F /PID <pid>" >&2
    fi
    exit 1
  fi
done

$K get svc "$RELEASE-coliseum-api" >/dev/null 2>&1   || { echo "Service $RELEASE-coliseum-api not found in context $CONTEXT / namespace $NAMESPACE (is the chart installed?)" >&2; exit 1; }
$K port-forward "svc/$RELEASE-coliseum-api" 8080:80 >/dev/null 2>&1 &
$K port-forward "svc/$RELEASE-coliseum-mcp" 8082:80 >/dev/null 2>&1 &
if $K get svc "$RELEASE-coliseum-otel-lgtm" >/dev/null 2>&1; then
  $K port-forward "svc/$RELEASE-coliseum-otel-lgtm" 3000:3000 >/dev/null 2>&1 &
fi
READY=http://127.0.0.1:8080/healthz/ready
for i in $(seq 1 30); do curl -sf --connect-timeout 2 "$READY" >/dev/null && break; sleep 1; done
curl -sf --connect-timeout 2 "$READY" >/dev/null || { echo "API not reachable through the port-forward" >&2; exit 1; }

echo "API:      http://localhost:8080   (arena: /arena/?name=Ata&auto=1  back-office: /backoffice/  docs: /scalar)"
echo "MCP:      http://localhost:8082/mcp   (X-Api-Key: dev-mcp-key)"
echo "Grafana:  http://localhost:3000   (admin / admin, dashboard Coliseum)"
echo "Port-forwards keep running in the background of this shell; stop them with: pkill -f port-forward"
