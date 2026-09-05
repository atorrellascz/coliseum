# ADR-0004: Deterministic randomness seeded by the battle id

- Status: Accepted
- Date: 2026-09-03

## Context and decision
The engine needs randomness for hit/miss rolls and the loot percentage, but the system needs to reprocess a
battle after a crash and get the same result, and tests need frozen outcomes. The engine therefore takes an
`IBattleRandom`; production uses xoshiro256** seeded with FNV-1a(battle id) expanded through SplitMix64.
`System.Random` is never used: its algorithm is not guaranteed stable across .NET versions.

## Consequences
- Same battle id + same players = same report, on every platform and in any language that implements the same
  three public algorithms (a Unity client could predict the server's result).
- Bounded rolls use Lemire's multiply-then-reject method, so the 10,000-point dodge roll is exactly uniform.
- Golden tests freeze ten reports (`tests/Coliseum.RegressionTests/golden`); known-answer tests pin the PRNG, the
  seed expander and the hash to published vectors.
- The seed is public (it is in the report) and predictable by design: this is reproducibility, not a lottery.
  Anti-cheat would need server-side secret salt, which is out of scope.
