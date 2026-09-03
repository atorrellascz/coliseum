# ADR-0003: Idempotent battle settlement in a Lua script

- Status: Proposed
- Date: 2026-09-03

## Context and decision
apply_battle.lua checks battle status, debits/credits both players, updates the leaderboard and marks done atomically. Re-delivery is a no-op. No distributed locks.

## Consequences
_To be completed when the micro-project that implements it lands._
