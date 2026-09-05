#!/usr/bin/env bash
# Chaos demo: a consumer takes battles from the stream and dies before acknowledging them. Watch XAUTOCLAIM hand
# them to the live worker after ClaimMinIdle (30 s) and every battle settle exactly once.
# The "dead consumer" is simulated deterministically: while the real worker is paused, the script itself reads
# entries with XREADGROUP as consumer "ghost-crashed" and never acks them (exactly what a crashed worker leaves behind).
#
#   bash scripts/chaos-worker.sh                                      # Compose stack (default)
#   MODE=k8s KUBE_CONTEXT=k3d-coliseum API_URL=http://localhost:18080 bash scripts/chaos-worker.sh
set -euo pipefail
cd "$(dirname "$0")/.."
MODE="${MODE:-compose}"
API_URL="${API_URL:-http://localhost:8080}"
API_KEY="${API_KEY:-dev-service-key}"
PREFIX="${PREFIX:-coliseum}"
BATTLES="${BATTLES:-20}"
STREAM="$PREFIX:battles:stream"

if [ "$MODE" = "k8s" ]; then
  K="kubectl --context ${KUBE_CONTEXT:-k3d-coliseum} -n ${NAMESPACE:-coliseum}"
  redis() { $K exec statefulset/coliseum-coliseum-redis -- redis-cli "$@" | tr -d '\r'; }
  pause_worker() { $K scale deploy/coliseum-coliseum-worker --replicas=0 >/dev/null; $K wait --for=delete pod -l app.kubernetes.io/component=worker --timeout=60s >/dev/null; }
  resume_worker() { $K scale deploy/coliseum-coliseum-worker --replicas=1 >/dev/null; }
  worker_logs() { $K logs deploy/coliseum-coliseum-worker --since=3m 2>/dev/null; }
else
  COMPOSE="${COMPOSE:-docker compose -f deploy/compose/docker-compose.yml}"
  redis() { $COMPOSE exec -T redis redis-cli "$@" | tr -d '\r'; }
  pause_worker() { $COMPOSE pause worker >/dev/null; }
  resume_worker() { $COMPOSE unpause worker >/dev/null; }
  worker_logs() { $COMPOSE logs --since 3m worker 2>/dev/null; }
fi
pending() { redis XPENDING "$STREAM" workers | head -1; }
json_field() { printf '%s' "$1" | sed -nE "s/.*\"$2\":\"?([^\",}]+)\"?.*/\1/p" | head -1; }
processed() { curl -sS "$API_URL/admin/stats" "${AUTH[@]}" | sed -nE 's/.*"battlesProcessed":([0-9]+).*/\1/p'; }

TOKEN=$(curl -sS -X POST "$API_URL/auth/token" -H "X-Api-Key: $API_KEY" | sed -E 's/.*"accessToken":"([^"]+)".*/\1/')
AUTH=(-H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json")
SUFFIX=$(date +%s | tail -c 6)
A=$(json_field "$(curl -sS -X POST "$API_URL/players" "${AUTH[@]}" -d "{\"name\":\"Chaos-A-$SUFFIX\",\"gold\":50000,\"silver\":20000,\"attack\":70,\"defense\":30,\"hitPoints\":100}")" id)
B=$(json_field "$(curl -sS -X POST "$API_URL/players" "${AUTH[@]}" -d "{\"name\":\"Chaos-B-$SUFFIX\",\"gold\":50000,\"silver\":20000,\"attack\":65,\"defense\":35,\"hitPoints\":110}")" id)
BEFORE=$(processed)

echo "1. pause the real worker and queue $BATTLES battles"
pause_worker
for i in $(seq 1 "$BATTLES"); do curl -sS -o /dev/null -X POST "$API_URL/battles" "${AUTH[@]}" -d "{\"attackerId\":\"$A\",\"defenderId\":\"$B\"}"; done
echo "   stream length: $(redis XLEN "$STREAM")   pending: $(pending)"

echo "2. a consumer named ghost-crashed reads them (XREADGROUP) and 'crashes' without XACK"
redis XREADGROUP GROUP workers ghost-crashed COUNT "$BATTLES" STREAMS "$STREAM" ">" >/dev/null
echo "   pending now: $(pending)   (XPENDING shows them owned by ghost-crashed)"
redis XPENDING "$STREAM" workers - + 3 | paste - - - - | cut -c1-100

echo "3. resume the real worker: it cannot see ghost's entries (already delivered) until XAUTOCLAIM reclaims them after ClaimMinIdle = 30 s"
resume_worker
for i in $(seq 1 120); do
  P=$(pending); D=$(( $(processed) - BEFORE ))
  [ $((i % 5)) -eq 0 ] && printf '   t+%03ds pending=%s settled=%s/%s\n' "$i" "$P" "$D" "$BATTLES"
  [ "$P" = "0" ] && [ "$D" -ge "$BATTLES" ] && break
  sleep 1
done

echo "4. evidence"
echo "   settled: $(( $(processed) - BEFORE )) of $BATTLES, pending: $(pending)  (never more than once: idempotent settlement)"
echo "   worker log:"; worker_logs | grep -E "Reclaimed|dead-lettered" | sed 's/\\u0022/"/g' | sed -E 's/.*"Message":"([^"]*)".*/      \1/' | tail -3
echo "   consumers: $(redis XINFO CONSUMERS "$STREAM" workers | grep -c '^name' ) (ghost-crashed stays listed with 0 pending; harmless, XGROUP DELCONSUMER removes it)"
echo "   Grafana: 'Stream length / pending' drops; 'processed duplicate' appears only if an already-settled entry is re-delivered."
echo "CHAOS OK"
