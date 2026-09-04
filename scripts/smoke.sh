#!/usr/bin/env bash
# End-to-end smoke test against a running stack (API + worker + Redis).
#   API_URL=http://localhost:8080 API_KEY=dev-service-key bash scripts/smoke.sh
# Flow: API key -> service token -> 3 players -> 5 battles -> wait until done -> leaderboard -> assert accounting.
# Dependencies: bash, curl. No jq: the tiny JSON fields we need are extracted with sed.
set -euo pipefail

API_URL="${API_URL:-http://localhost:8080}"
API_KEY="${API_KEY:-dev-service-key}"
TIMEOUT_SECONDS="${TIMEOUT_SECONDS:-30}"

json_field() { # $1=json $2=field  -> first scalar value of "field"
  printf '%s' "$1" | sed -nE "s/.*\"$2\":\"?([^\",}]+)\"?.*/\1/p" | head -1
}

fail() { echo "SMOKE FAILED: $*" >&2; exit 1; }

echo "1. token"
TOKEN_JSON=$(curl -sS -X POST "$API_URL/auth/token" -H "X-Api-Key: $API_KEY")
TOKEN=$(json_field "$TOKEN_JSON" accessToken)
[ -n "$TOKEN" ] || fail "no token: $TOKEN_JSON"
AUTH=(-H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json")

echo "2. players"
SUFFIX=$(date +%s | tail -c 6)
declare -a IDS
for NAME in Ata Bot Cleo; do
  BODY=$(printf '{"name":"%s-%s","description":"smoke","gold":10000,"silver":5000,"attack":70,"defense":30,"hitPoints":100}' "$NAME" "$SUFFIX")
  RESP=$(curl -sS -X POST "$API_URL/players" "${AUTH[@]}" -d "$BODY")
  ID=$(json_field "$RESP" id)
  [ -n "$ID" ] || fail "player not created: $RESP"
  IDS+=("$ID")
  echo "   $NAME -> $ID"
done

echo "3. battles"
declare -a BATTLES
for i in 0 1 2 3 4; do
  ATT=${IDS[$((i % 3))]}; DEF=${IDS[$(((i + 1) % 3))]}
  RESP=$(curl -sS -X POST "$API_URL/battles" "${AUTH[@]}" -d "{\"attackerId\":\"$ATT\",\"defenderId\":\"$DEF\"}")
  BID=$(json_field "$RESP" battleId)
  [ -n "$BID" ] || fail "battle not accepted: $RESP"
  BATTLES+=("$BID")
  echo "   queued $BID"
done

echo "4. wait for settlement"
TOTAL_LOOT=0
for BID in "${BATTLES[@]}"; do
  DEADLINE=$(( $(date +%s) + TIMEOUT_SECONDS ))
  while :; do
    REPORT=$(curl -sS "$API_URL/battles/$BID" "${AUTH[@]}")
    STATUS=$(json_field "$REPORT" status)
    if [ "$STATUS" = "done" ]; then
      SCORE=$(printf '%s' "$REPORT" | sed -nE 's/.*"loot":\{[^}]*"score":([0-9]+).*/\1/p')
      WINNER=$(json_field "$REPORT" winnerId)
      echo "   $BID done: winner $WINNER, score $SCORE"
      TOTAL_LOOT=$((TOTAL_LOOT + SCORE))
      break
    fi
    [ "$STATUS" = "failed" ] && fail "battle $BID failed: $REPORT"
    [ "$(date +%s)" -lt "$DEADLINE" ] || fail "battle $BID still $STATUS after ${TIMEOUT_SECONDS}s"
    sleep 0.5
  done
done

echo "5. leaderboard"
BOARD=$(curl -sS "$API_URL/leaderboard?limit=100" "${AUTH[@]}")
BOARD_SUM=0
for S in $(printf '%s' "$BOARD" | grep -oE '"score":[0-9]+' | grep -oE '[0-9]+'); do
  BOARD_SUM=$((BOARD_SUM + S))
done
# Other smoke runs may have added scores; the board must be at least what we produced, and never less.
[ "$BOARD_SUM" -ge "$TOTAL_LOOT" ] || fail "leaderboard sum $BOARD_SUM < loot produced $TOTAL_LOOT"
echo "   leaderboard total $BOARD_SUM >= loot produced $TOTAL_LOOT"

echo "SMOKE OK"
