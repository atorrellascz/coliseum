# ADR-0007: Integer-only arithmetic in the engine

- Status: Accepted
- Date: 2026-09-03

## Context and decision
The spec's examples are integers (70 attack at 90 % health → 63; 7 % of 120 silver → 9, rounded up). Floating
point would introduce platform- and language-dependent rounding and make replays and golden files fragile.
Every formula uses `long` arithmetic: `ceil(v·p/100) = (v·p + 99) / 100`, `floor(base·hp/maxHp)`, dodge chance in
basis points with integer division.

## Consequences
- Bit-identical results between the C# engine, the Lua settlement and the tests; the spec examples are literal
  unit tests.
- Value ranges are chosen so no intermediate overflows a 64-bit integer (1e9 × 100 for loot; 10,000 × 10,000 for
  attack scaling).
- Percentages are integers (5..10) by design; a rule change to fractional percentages would need basis points,
  which the dodge formula already demonstrates.
