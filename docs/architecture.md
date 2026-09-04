# Coliseum — Software Design Document

This is the design of record. Decisions are justified in `docs/adr/`; assumptions are listed at the end.

## 1. Purpose

A backend that registers players, queues battles between them, simulates the battles with a deterministic
engine, settles the loot, and ranks players by the resources they have stolen. Three hosts share one Redis:

```
                +----------------+        HTTP (bearer)        +----------------+
  agents  ----> |  Coliseum.Mcp  | --------------------------> |  Coliseum.Api  | <---- players / JS client
                +----------------+                             +-------+--------+
                                                                       | create player (Lua, SET NX)
                                                                       | submit battle: HSET battle:{id} + XADD
                                                                       | read report: HGETALL, leaderboard: ZREVRANGE
                                                                       v
                                                              +-----------------+
                                                              |      Redis      |  hashes, sorted set, streams, pub/sub
                                                              +--------+--------+
                                                                       ^
                                                                       | XREADGROUP / XAUTOCLAIM / XACK, EVALSHA apply_battle
                                                              +--------+--------+
                                                              | Coliseum.Worker |  BattleScheduler + BattleEngine
                                                              +-----------------+
```

## 2. Projects and the dependency rule

| Project | Responsibility | May reference |
|---------|----------------|---------------|
| `Coliseum.Domain` | Entities, value objects, `BattleEngine`, deterministic PRNG. Zero packages. | nothing |
| `Coliseum.Contracts` | DTOs and live events shared by API, MCP, clients. Zero packages. | nothing |
| `Coliseum.Application` | Ports (interfaces), use cases, `BattleScheduler`, telemetry instruments. | Domain, Contracts |
| `Coliseum.Infrastructure.Redis` | One adapter per port, Lua scripts, key schema, health check. | Application |
| `Coliseum.ServiceDefaults` | OpenTelemetry, health endpoints, logging, resilient HttpClient. | Application (names only) |
| `Coliseum.Api` / `Coliseum.Worker` / `Coliseum.Mcp` | Composition roots. | everything below them |

Dependencies point inward. `tests/Coliseum.UnitTests/Architecture/DependencyRulesTests.cs` reads the project files
and fails the build if the rule is broken.

## 3. Domain model

- **Player** (aggregate root): id, name (≤ 20, unique case-insensitively), description (≤ 1,000), `Resources`
  (gold and silver, each in [0, 1e9]), `CombatStats` (attack 1..10,000, defense 0..10,000, hit points 1..10,000).
  `Player.Create` validates everything and returns every violation; `Player.Rehydrate` trusts storage.
- **Battle engine** (`BattleEngine.Run`): pure function of (battle id, attacker, defender, rules).
  - Initiator strikes first; roles alternate.
  - Attack decays with health: `max(ceil(base·50%), floor(base·hp/maxHp))`. Spec example 70/100 at 90 hp → 63, floor 35.
  - Dodge chance = `defense / (defense + attack)` in basis points, capped at 75 %. A roll in [0, 10,000) below it misses.
  - Battle ends when a fighter reaches 0 hp. A `MaxTurns` guard (100,000) returns an invariant error instead of looping.
  - Loot: one percentage in [5, 10] drawn after the fight, applied to each resource with integer ceil. Spec example
    500/120 at 7 % → 35/9.
  - Randomness: xoshiro256** seeded with FNV-1a(battle id). Same id, same battle. Golden tests freeze ten reports.
- **BattleReport**: seed, participants, winner/loser, every `TurnEvent` (hp before/after, attack used, dodge, roll,
  hit, damage), loot. Enough to audit and replay.

## 4. Use cases and authorization

| Use case | Caller rule |
|----------|-------------|
| Create player | service token only (endpoint policy) |
| Submit battle | player token attacks as itself; service token names the attacker; self-battle rejected |
| Get battle | participants or service; others get 404 (existence is not revealed) |
| Leaderboard, get player | any token |

Hosts map the JWT to a `Caller`; use cases enforce the data-dependent rules (ADR-0015).

## 5. Redis data model

See `docs/redis-data-model.md` for every key. Highlights:

- `create_player.lua`: `SET NX` on the normalized name, then `HSET` the player. Two concurrent registrations with
  the same name: exactly one wins, atomically.
- `battle:{id}` is written in state `queued` **before** `XADD` (PAT-10): a crash between the two leaves a visible
  record and never an orphan message.
- `apply_battle.lua` (ADR-0003): if `status == done`, return the original amounts and touch nothing; else compute
  loot on the loser's live balance, debit, credit (capped), `ZINCRBY` the leaderboard, store the report, mark done.
  One atomic round trip, idempotent on the battle id.
- `mark_battle.lua` never regresses a `done` battle to `processing`/`failed`.

## 6. Queue guarantees

| Requirement | Mechanism |
|-------------|-----------|
| In submission order | Stream ids are monotonic; the worker reads them in order and the scheduler preserves per-player order |
| None skipped | `XACK` only after settlement; a crash leaves the entry pending; `XAUTOCLAIM` (every 15 s, idle ≥ 30 s) hands it to a live consumer; after 5 deliveries it goes to `battles:dlq` and is acknowledged, so it is never silently lost |
| None twice | Delivery is at-least-once; the settlement is idempotent; re-processing yields `Duplicate` and changes nothing |
| Immediate | Non-blocking reads with a 250 ms poll when idle (ADR-0016) |

## 7. Concurrency

`BattleScheduler` (ADR-0005) runs in a single-writer loop inside the worker: battles whose two players are free
start in parallel (bounded by `MaxConcurrency`); a blocked battle reserves its players so no later battle involving
them overtakes it. Verified by scenario tests and a 400-battle random simulation. Data consistency does not depend
on the scheduler: the Lua settlement is atomic on its own.

## 8. Security

- Every business endpoint requires `Authorization: Bearer <JWT>` (HS256, issuer/audience/lifetime validated).
- `POST /auth/token` exchanges an API key (constant-time compare) for a 24 h service token; creating a player
  returns a 1 h player token.
- Rate limiting per token (hashed) or IP: 100 requests / 10 s, `429` with `Retry-After`.
- Request body limit 64 KB, conservative headers, explicit CORS allow-list, ids validated before touching a key.
- The MCP HTTP transport requires its own `X-Api-Key`; the MCP server itself is just a client of the API.

## 9. Observability

One `ActivitySource`/`Meter` named `coliseum`. Metrics (Prometheus names): `coliseum_battles_submitted_total`,
`coliseum_battles_processed_total{result}`, `coliseum_battle_processing_duration_seconds`,
`coliseum_battle_queue_latency_seconds`, `coliseum_battle_turns`, `coliseum_resources_stolen_total{resource}`,
`coliseum_scheduler_running`, `coliseum_scheduler_pending`, `coliseum_queue_length`, `coliseum_queue_pending`,
`coliseum_dlq_length`. Ids never appear as metric tags. `/healthz/live`, `/healthz/ready` (Redis PING, worker
loop heartbeat) and `/metrics` on every host; OTLP export when `OTEL_EXPORTER_OTLP_ENDPOINT` is set.

## 10. Balance observation

With identical stats (70 attack, 30 defense, 100 hp) the attacker wins about 72 % of simulations: striking first
is a large advantage under the spec's rules. The engine exposes this through `estimate_win_chance` (MCP) and the
back-office will chart attacker win rate (MP-08). Changing the rules is a `BattleRules` change guarded by the golden tests.

## 11. Assumptions (SUP)

| ID | Assumption |
|----|------------|
| SUP-01/13 | Dodge = `def / (def + atk)`, capped at 75 % |
| SUP-02 | xoshiro256** seeded from the battle id; `System.Random` is never used |
| SUP-03 | One loot percentage per battle, applied per resource with ceil |
| SUP-04 | Loot is recomputed on the live balance at settlement |
| SUP-05 | Excess above 1e9 on the winner is burned |
| SUP-06 | Names are unique ignoring case and surrounding spaces |
| SUP-07 | One worker gives strict global order; scale by partitioning the stream |
| SUP-08 | Players are not deleted in v1; a missing player at processing time fails the battle |
| SUP-09 | HS256 with a shared secret; a corporate IdP is a one-adapter swap |
| SUP-10 | Redis runs with `maxmemory-policy noeviction` and AOF |
| SUP-11 | Stats capped at 10,000 to bound turns and report size |
| SUP-12 | `MaxTurns` guard fails the battle instead of throwing |
| SUP-14 | Loot percentage drawn after the fight so it never alters the turn sequence |
| SUP-15 | Stream reads poll (StackExchange.Redis has no blocking reads); latency ≤ 250 ms idle |
