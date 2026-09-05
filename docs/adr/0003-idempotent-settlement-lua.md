# ADR-0003: Idempotent battle settlement in a Lua script

- Status: Accepted
- Date: 2026-09-03

## Context and decision
A message can be delivered twice (crash after processing, before `XACK`). Instead of trying to prevent
re-delivery with distributed locks (expiry bugs, extra round trips), the settlement is made idempotent and atomic:
`apply_battle.lua` reads `battle:{id}.status`; if it is already `done` it returns the original amounts and touches
nothing; otherwise it computes the loot on the loser's live balance, debits, credits (capped at 1e9), increments
the leaderboard, stores the report and marks `done`. Redis executes the script without interleaving other commands.
`MULTI/EXEC` cannot branch on a value it reads, which is why a script and not a transaction.

## Consequences
- The battle id is the idempotency key. Reprocessing yields `SettlementOutcome.AlreadyApplied`; the worker counts
  it under `coliseum_battles_processed_total{result="duplicate"}`, a useful signal of crash loops.
- Loot amounts in the report are what was actually transferred at settlement time (SUP-04), which may differ from
  the engine's estimate if the loser's balance changed in between.
- `mark_battle.lua` protects the same invariant from the other side: a late `processing`/`failed` mark never
  regresses a `done` battle.
- The integer formulas are duplicated in C# (`IntegerMath`) and Lua on purpose and pinned by tests on both sides.
- Requires `noeviction` on Redis (ADR-0001).
