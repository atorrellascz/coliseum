# ADR-0004: Deterministic RNG seeded by battle id

- Status: Proposed
- Date: 2026-09-03

## Context and decision
xoshiro256** seeded from FNV-1a(battleId). Same input, same report: safe reprocessing, replays, golden tests. System.Random is banned.

## Consequences
_To be completed when the micro-project that implements it lands._
