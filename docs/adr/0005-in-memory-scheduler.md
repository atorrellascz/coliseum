# ADR-0005: In-memory single-writer scheduler for concurrency

- Status: Accepted (implemented in MP-04)
- Date: 2026-09-03

## Context
The spec asks for "simultaneous processing of battles that don't involve overlapping players" while battles
are "handled in the order they are submitted". Distributed locks per player would allow a later battle to grab a
lock before an earlier one and would add a network round trip per battle.

## Decision
`BattleScheduler` (Application layer) keeps a pending list in stream order, a `busy` set of players in running
battles, and a per-dispatch `reserved` set. `Dispatch()` walks the pending list front to back: a battle starts
when neither player is busy or reserved; otherwise it reserves both players so no later battle involving them
can overtake it. Concurrency is capped by `MaxConcurrency`. One loop owns the scheduler (single-writer); it
holds no locks and does no I/O.

## Consequences
- Guarantees are testable in isolation: hand-built scenarios plus a 400-battle / 12-player random simulation
  assert "no shared player among running battles" and "per-player start order equals submission order".
- Data atomicity is not the scheduler's job; the Lua settlement (ADR-0003) provides it, so the scheduler only
  needs to be correct about ordering and overlap.
- With several worker replicas the ordering guarantee is per worker (ADR-0006); scaling keeps order by
  partitioning the stream by player.
