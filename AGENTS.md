# AGENTS.md — how AI is used in this repository

This repository is built with an AI pair (Claude Code) acting as a senior backend / SRE / DBA reviewer.
The rules below apply to any agent or human contributing.

## Ground rules
- Every engine formula and every Lua script is verified by hand against the examples in the spec before merge.
- The AI proposes; the author reviews every generated file and decides. Nothing lands unread.
- Tests are written to fail first when a rule changes (golden files, property tests), so regressions are caught even when the AI edits code.
- Private planning notes live outside the repo (`_referencia/`, git-ignored). Public design lives in `docs/adr`.

## Working conventions for agents
- Language: code and comments in English; commit messages in English.
- Do not add a NuGet package without adding its version to `Directory.Packages.props`.
- `Coliseum.Domain` and `Coliseum.Contracts` must stay dependency-free (architecture test).
- Keep `Program.cs` files as composition roots only.
- Log every meaningful step in `docs/DEVLOG.md`.

## MCP
`src/Coliseum.Mcp` is a Model Context Protocol server so an agent can play the game (create players, submit battles,
read reports, read the leaderboard) and run what-if simulations locally with the same engine the server uses.
