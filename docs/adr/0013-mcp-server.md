# ADR-0013: MCP server as a first-class host

- Status: Accepted (implemented in MP-06b)
- Date: 2026-09-03

## Context and decision
The company is AI-forward and wants agents to operate the game. `Coliseum.Mcp` exposes the game through the
Model Context Protocol using the official C# SDK: Streamable HTTP on `/mcp` (protected by an API key) or stdio for
local clients. The server is a client of the HTTP API (service token, cached), so validation, authorization and
rate limits apply to agents exactly as to humans; what-if tools run the Domain engine locally with no side effects.

## Consequences
- Eight tools: `create_player`, `get_player`, `submit_battle`, `get_battle_report`, `play_battle` (submit and
  wait), `get_leaderboard`, `simulate_battle`, `estimate_win_chance`.
- Problem Details from the API are translated into readable tool errors that name the offending fields, which is
  what lets an agent self-correct.
- The MCP host has no Redis dependency and no business logic; it can be scaled or removed independently.
- The protocol layer is the SDK's responsibility; upgrading it (e.g. new transport versions) does not touch tools.
