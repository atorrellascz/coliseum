# MCP server

`Coliseum.Mcp` exposes the game to AI agents through the Model Context Protocol (official C# SDK). It is a
client of the HTTP API, so every rule the API enforces (validation, authorization, rate limits) applies to
agents too. What-if tools run the same battle engine locally with no side effects (ADR-0013).

## Tools

| Tool | What it does | Side effects |
|------|--------------|--------------|
| `create_player` | Creates a player (name, stats, gold, silver) | yes |
| `get_player` | Reads a profile | no |
| `submit_battle` | Queues a battle and returns immediately | yes |
| `get_battle_report` | Reads status or the full report with narrative | no |
| `play_battle` | Submits and waits (≤ 60 s) for the settled report | yes |
| `get_leaderboard` | Ranked players with rank, score, player id | no |
| `simulate_battle` | One local simulation for given stats and seed | no |
| `estimate_win_chance` | N local simulations, attacker win rate and average turns | no |

## Transports

- **Streamable HTTP** (default): `POST/GET /mcp`. Requires header `X-Api-Key: <Mcp:ClientApiKey>`.
  `/healthz/*` and `/metrics` are open for probes.
- **stdio**: `dotnet run --project src/Coliseum.Mcp -- --stdio`. Logs go to stderr; stdout carries the protocol.

## Configuration (section `Mcp`)

| Key | Meaning |
|-----|---------|
| `ApiBaseUrl` | Where the Coliseum API lives |
| `ApiKey` | Exchanged for a service token at `POST /auth/token` |
| `ClientApiKey` | Key MCP clients must present to the HTTP transport |

Environment variables: `Mcp__ApiBaseUrl`, `Mcp__ApiKey`, `Mcp__ClientApiKey`.

## Claude Desktop (stdio) example

```json
{
  "mcpServers": {
    "coliseum": {
      "command": "dotnet",
      "args": ["run", "--project", "C:/Evolution/coliseum/src/Coliseum.Mcp", "--", "--stdio"],
      "env": { "Mcp__ApiBaseUrl": "http://localhost:8080", "Mcp__ApiKey": "dev-service-key", "Mcp__ClientApiKey": "dev-mcp-key", "DOTNET_ENVIRONMENT": "Development" }
    }
  }
}
```

## Smoke check over HTTP

```bash
curl -s -X POST http://localhost:8082/mcp -H "X-Api-Key: dev-mcp-key" -H "Content-Type: application/json" \
  -H "Accept: application/json, text/event-stream" \
  -d '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"curl","version":"0"}}}'
```
