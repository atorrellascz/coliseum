#!/usr/bin/env bash
# RB-02: move dead-lettered battles back to the main stream after the cause is fixed.
# Settlement is idempotent, so replaying an already settled battle is harmless.
#   REDIS_CLI="docker compose -f deploy/compose/docker-compose.yml exec -T redis redis-cli" bash scripts/replay-dlq.sh
#   REDIS_CLI="redis-cli -h my-redis" PREFIX=coliseum bash scripts/replay-dlq.sh
set -euo pipefail
REDIS_CLI="${REDIS_CLI:-redis-cli}"
PREFIX="${PREFIX:-coliseum}"
DLQ="$PREFIX:battles:dlq"
STREAM="$PREFIX:battles:stream"

count=$($REDIS_CLI XLEN "$DLQ" | tr -d '\r')
echo "dead-lettered entries: $count"
[ "$count" -gt 0 ] || exit 0

# XRANGE output: id line, then field/value lines in pairs. Re-add the four original fields, then delete the DLQ entry.
$REDIS_CLI --raw XRANGE "$DLQ" - + | awk -v cli="$REDIS_CLI" -v stream="$STREAM" -v dlq="$DLQ" '
  /^[0-9]+-[0-9]+$/ { if (id != "") flush(); id = $0; n = 0; next }
  { kv[n++] = $0 }
  function flush(   f, i, cmd) {
    for (i = 0; i < n; i += 2) f[kv[i]] = kv[i + 1]
    cmd = cli " XADD " stream " * battleId " f["battleId"] " attackerId " f["attackerId"] " defenderId " f["defenderId"] " submittedAt " f["submittedAt"]
    system(cmd " >/dev/null && " cli " XDEL " dlq " " id " >/dev/null && echo replayed " f["battleId"])
    delete f
  }
  END { if (id != "") flush() }'
