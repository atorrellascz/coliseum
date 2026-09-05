# ADR-0011: Domain and Contracts are packaged as NuGet packages

- Status: Accepted
- Date: 2026-09-03

## Context and decision
The battle engine is a pure, dependency-free library. Packaging `Coliseum.Domain` (engine, value objects) and
`Coliseum.Contracts` (DTOs, events) lets other processes reuse them: a Unity client predicting a battle locally, a
balance notebook running 100,000 simulations, or the MCP server's `simulate_battle` and `estimate_win_chance`
tools, which already do exactly that inside this repository.

## Consequences
- `release.yml` packs both projects with the tag version and pushes them to GitHub Packages; SourceLink is enabled
  in CI so consumers can step into the code.
- Both packages target `net10.0` only. Unity would need a `netstandard2.1` target and a review of BCL APIs
  (`Math.BigMul`, `Guid.CreateVersion7` is not used in Domain); documented, not done.
- The architecture test guarantees the packages stay dependency-free, which is what makes them reusable.
