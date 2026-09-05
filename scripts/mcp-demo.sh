#!/usr/bin/env bash
# MCP walkthrough over Streamable HTTP with curl: initialize -> tools/list -> create two players -> play a battle
# -> leaderboard -> local what-if. The same JSON bodies work in Postman (POST /mcp, headers below).
#   MCP_URL=http://localhost:8082/mcp MCP_KEY=dev-mcp-key bash scripts/mcp-demo.sh
set -euo pipefail
MCP_URL="${MCP_URL:-http://localhost:8082/mcp}"
MCP_KEY="${MCP_KEY:-dev-mcp-key}"
H=(-H "X-Api-Key: $MCP_KEY" -H "Content-Type: application/json" -H "Accept: application/json, text/event-stream")
rpc() { # $1=json ; prints the SSE data payload
  curl -sS -X POST "$MCP_URL" "${H[@]}" ${SID:+-H "Mcp-Session-Id: $SID"} -d "$1" | sed -n 's/^data: //p'
}
text() { printf '%s' "$1" | sed -nE 's/.*"text":"((\\.|[^"\\])*)".*/\1/p' | sed 's/\\u0022/"/g; s/\\"/"/g'; }
field() { printf '%s' "$1" | sed -nE "s/.*\"$2\":\"?([^\",}]+)\"?.*/\1/p" | head -1; }

echo "1. initialize (a stateful server answers with an Mcp-Session-Id header; this one runs stateless, so none is needed)"
INIT=$(curl -sS -D /tmp/mcp-headers.txt -X POST "$MCP_URL" "${H[@]}" -d '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"demo","version":"1"}}}')
printf '%s' "$INIT" | grep -q '"serverInfo"' || { echo "initialize failed: is the MCP server up at $MCP_URL and the key right?"; printf '%s\n' "$INIT" | head -3; exit 1; }
SID=$(grep -i "^mcp-session-id:" /tmp/mcp-headers.txt 2>/dev/null | tr -d '\r' | awk '{print $2}' || true)
rpc '{"jsonrpc":"2.0","method":"notifications/initialized"}' >/dev/null || true
echo "   server: $(printf '%s' "$INIT" | grep -oE '"serverInfo":\{[^}]*\}')  session: ${SID:-stateless}"

echo "2. tools/list"
rpc '{"jsonrpc":"2.0","id":2,"method":"tools/list"}' | grep -oE '"name":"[a-z_]+"' | tr '\n' ' '; echo

echo "3. estimate_win_chance (local simulation, no side effects): 70/30/100 vs 60/40/120"
text "$(rpc '{"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"estimate_win_chance","arguments":{"attackerAttack":70,"attackerDefense":30,"attackerHitPoints":100,"defenderAttack":60,"defenderDefense":40,"defenderHitPoints":120,"simulations":500}}}')"; echo

S=$(date +%s | tail -c 5)
echo "4. create_player x2 (through the API: validation, auth and rate limits apply)"
P1=$(text "$(rpc "{\"jsonrpc\":\"2.0\",\"id\":4,\"method\":\"tools/call\",\"params\":{\"name\":\"create_player\",\"arguments\":{\"name\":\"Agent-$S\",\"attack\":70,\"defense\":30,\"hitPoints\":100,\"gold\":3000,\"silver\":900}}}")")
P2=$(text "$(rpc "{\"jsonrpc\":\"2.0\",\"id\":5,\"method\":\"tools/call\",\"params\":{\"name\":\"create_player\",\"arguments\":{\"name\":\"Target-$S\",\"attack\":60,\"defense\":40,\"hitPoints\":120,\"gold\":3000,\"silver\":900}}}")")
A=$(field "$P1" id); B=$(field "$P2" id)
echo "   $A vs $B"

echo "5. play_battle (submit + wait for the worker) -> outcome"
R=$(text "$(rpc "{\"jsonrpc\":\"2.0\",\"id\":6,\"method\":\"tools/call\",\"params\":{\"name\":\"play_battle\",\"arguments\":{\"attackerId\":\"$A\",\"defenderId\":\"$B\",\"timeoutSeconds\":20}}}")")
echo "   status=$(field "$R" status) winner=$(field "$R" winnerId) turns=$(field "$R" turns) seed=$(field "$R" seed)"
printf '%s' "$R" | grep -oE '"narrative":\[[^]]*' | sed 's/","/"\n   /g' | head -4

echo "6. get_leaderboard"
text "$(rpc '{"jsonrpc":"2.0","id":7,"method":"tools/call","params":{"name":"get_leaderboard","arguments":{"limit":5}}}')" | cut -c1-300; echo
echo "Watch it land: back-office feed and leaderboard, Grafana 'submitted vs processed', worker logs. MCP OK"
