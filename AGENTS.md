# AGENTS.md — how AI was used in this repository

This repository was built by its author pair-programming with Claude Code (Anthropic) over three days. The
division of labour, so the reader can judge what to trust and how.

## What the AI did
- Turned a written plan (requirements analysis, architecture, micro-project roadmap, written by the author) into
  code, one micro-project at a time, in the order the author set.
- Wrote the first version of every file, including tests, Lua scripts, Helm templates, workflows and docs.
- Ran builds, tests, containers and clusters, and fixed what broke, recording each finding in `docs/DEVLOG.md`.

## What the author did
- Wrote the specification analysis and the architecture, chose Redis Streams, the Lua settlement, the deterministic
  engine and the scheduler design before any code existed.
- Reviewed every generated file (the review checklists are in `docs/TASKS.md`), redirected the work when it
  drifted (for example: "Program.cs stays composition only", which became an architecture test), and decided every
  trade-off listed in the README.
- Owns the interview: the reasoning behind each decision is documented so it can be defended, not recited.

## What was verified by hand or by machine, not taken on faith
- The engine formulas against the examples in the spec (unit tests are the literal examples).
- The PRNG, seed expander and hash against published reference vectors.
- The settlement script against a real Redis, including double application, floors and caps.
- Every host started for real (Compose, Docker Desktop Kubernetes, k3d) and hit with the smoke script; the tests
  alone missed three start-up/static-file bugs that the live runs caught.
- Package versions checked against nuget.org; action tags checked against GitHub (one wrong tag broke a CI run
  and was fixed the same hour).

## Rules for agents working on this repository
- Read `docs/TASKS.md` first; update it and `docs/DEVLOG.md` at every step.
- Code and comments in English. No business logic in `Program.cs`. No new NuGet package without a line in
  `Directory.Packages.props`. `Coliseum.Domain` and `Coliseum.Contracts` stay dependency-free.
- Every change ships with tests; `dotnet build` must stay at zero warnings and `dotnet format` clean.
- Never point `kubectl` at a cluster without an explicit `--context`.
- The MCP server (`src/Coliseum.Mcp`) is how an agent should play the game; it inherits the API's rules.
