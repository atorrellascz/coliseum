# TASKS

Single source of truth for progress. Updated at every step; `docs/DEVLOG.md` keeps the history, this file keeps the state.
Legend: `[ ]` todo · `[~]` in progress · `[x]` done · `[-]` dropped (reason in DEVLOG).

## Milestones

| MP | Scope | Status | Commit |
|----|-------|--------|--------|
| MP-01 | Terraform (AWS: vpc, eks, ecr, elasticache, secrets, iam), validated not applied | [ ] | |
| MP-02 | Solution skeleton, CPM, analyzers, stubs, docs placeholders | [x] | c868c18, 7080a1d |
| MP-03 | Domain: value objects, Player aggregate, PRNG, BattleEngine, unit + golden tests | [x] code done, review pending | see DEVLOG |
| MP-04 | Application: ports, use cases, BattleScheduler, telemetry, fakes | [x] code done, review pending | see DEVLOG |
| MP-05 | Redis adapters, Lua scripts, integration tests (Testcontainers) | [ ] | |
| MP-06 | API + Worker hosts, ServiceDefaults, auth, smoke script | [ ] | |
| MP-06b | MCP server (tools over the API + local simulation) | [ ] | |
| MP-07 | SignalR hub + arena auto-play client | [ ] | |
| MP-08 | Back-office (RED / USE / economy) + admin stats | [ ] | |
| MP-09 | Dockerfile, Compose (+ Grafana), Helm, k3d comparison | [ ] | |
| MP-10 | GitHub Actions CI + release | [ ] | |
| MP-11 | Argo CD + Rollouts canary | [ ] | |
| MP-12 | Final docs, video, tag v1.0.0 | [ ] | |

## MP-03 checklist (review one by one)

Domain (`src/Coliseum.Domain`)
- [x] Common/DomainError.cs — error kinds + factories
- [x] Common/Result.cs — Result<T> + non-generic factories
- [x] Common/Identifier.cs — shared id format rule
- [x] Common/IntegerMath.cs — CeilPercent / FloorPercent
- [x] Players/PlayerId.cs
- [x] Players/PlayerName.cs — rules + Normalize
- [x] Players/Resources.cs — value object, saturating math, Percent
- [x] Players/CombatStats.cs — value object, caps (SUP-11)
- [x] Players/Player.cs — aggregate, Create / Rehydrate / WithResources
- [x] Randomness/IBattleRandom.cs
- [x] Randomness/Xoshiro256StarStar.cs — PRNG + SplitMix64 + Lemire bounded roll
- [x] Randomness/SeedDerivation.cs — FNV-1a 64
- [x] Battles/BattleId.cs
- [x] Battles/BattleRules.cs — tunables + Validate (SUP-12 turn guard)
- [x] Battles/Combatant.cs — internal scratch state
- [x] Battles/TurnEvent.cs
- [x] Battles/LootResult.cs
- [x] Battles/BattleReport.cs
- [x] Battles/BattleEngine.cs — Run / CurrentAttack / DodgeBasisPoints

Tests
- [x] UnitTests/Domain/TestPlayers.cs — builder
- [x] UnitTests/Fakes/SequenceRandom.cs — scripted rolls
- [x] UnitTests/Domain/PlayerTests.cs — boundaries
- [x] UnitTests/Domain/BattleEngineTests.cs — spec examples + rules
- [x] UnitTests/Domain/BattleEnginePropertyTests.cs — 2,000 random battles
- [x] UnitTests/Domain/Xoshiro256StarStarTests.cs — known-answer tests
- [x] UnitTests/Architecture/DependencyRulesTests.cs — dependency rule (MP-02 leftover)
- [x] RegressionTests/GoldenBattleTests.cs + golden/*.json

Verification for MP-03 DoD
- [x] `dotnet build` Domain + tests: 0 warnings
- [x] `dotnet test` UnitTests (72) + RegressionTests (10 golden) green
- [ ] User reviewed every file above
- [x] Commit `MP-03: domain and battle engine`

Tooling lesson learned in MP-03 (see ADR-14): `dotnet test` in Microsoft.Testing.Platform mode forwards unknown
flags to the test app; `--nologo` makes it exit with code 5 and report "Zero tests ran". Use `--project` / `--solution`.

## MP-04 checklist (review one by one)

Contracts (`src/Coliseum.Contracts`) — wire types, no logic
- [x] Players/CreatePlayerRequest.cs, PlayerResponse.cs, CreatePlayerResponse.cs
- [x] Battles/SubmitBattleRequest.cs, BattleStatus.cs, BattleSubmittedResponse.cs, BattleReportResponse.cs (+ TurnResponse, LootResponse)
- [x] Leaderboard/LeaderboardEntry.cs, LeaderboardResponse.cs
- [x] Auth/TokenResponse.cs (TokenRequest dropped: the API key travels in a header)
- [x] Errors/ApiProblem.cs — RFC 9457 shape + errors[]
- [x] Events/ArenaEvents.cs — polymorphic JSON events (battle.queued / turn / done / failed, leaderboard.changed)

Application (`src/Coliseum.Application`)
- [x] Caller.cs — who calls (Service | Player), used for data-dependent authorization
- [x] Ports/IPlayerRepository.cs, IBattleQueue.cs (+ QueuedBattle, QueueStats), IBattleLedger.cs (+ SettlementResult), IBattleReportStore.cs (+ BattleRecord), ILeaderboard.cs, IAuthTokenService.cs, IEventPublisher.cs, IClock.cs, IIdGenerator.cs
- [x] Mapping/PlayerMapper.cs, BattleMapper.cs
- [x] UseCases/Players/CreatePlayerHandler.cs, GetPlayerHandler.cs
- [x] UseCases/Battles/SubmitBattleHandler.cs, GetBattleHandler.cs, ProcessBattleHandler.cs, BattleNarrator.cs
- [x] UseCases/Leaderboard/GetLeaderboardHandler.cs
- [x] Scheduling/BattleScheduler.cs, ScheduledBattle.cs — no overlap, no overtaking, bounded concurrency
- [x] Options/BattleRulesOptions.cs (+ validator), AuthOptions.cs
- [x] Telemetry/ColiseumTelemetry.cs — ActivitySource + Meter + instruments (OPS-02)
- [x] DependencyInjection.cs — AddColiseumApplication / AddBattleScheduler
- [ ] Ports/IGameStats.cs — left as stub for MP-08

Tests (`tests/Coliseum.UnitTests`)
- [x] Fakes/FixedClock.cs (+ SequentialIdGenerator, FakeTokenService), InMemoryPlayerRepository.cs, InMemoryBattleQueue.cs, InMemoryBattleLedger.cs, InMemoryLeaderboard.cs, InMemoryBattleReportStore.cs, RecordingEventPublisher.cs, FakeWorld.cs
- [x] Application/BattleSchedulerTests.cs — 3 guarantees + 400-battle / 12-player random simulation
- [x] Application/CreatePlayerHandlerTests.cs, SubmitBattleHandlerTests.cs, ProcessBattleHandlerTests.cs, GetBattleHandlerTests.cs, GetLeaderboardHandlerTests.cs

Verification for MP-04 DoD
- [x] Contracts + Application + UnitTests build: 0 warnings; `dotnet format` clean
- [x] `dotnet test --project tests/Coliseum.UnitTests`: 103 / 103 (31 new)
- [x] Architecture test still green (Application references only Domain + Contracts)
- [ ] User reviewed every file above
- [x] Commit `MP-04: application layer`

Deferred from MP-04
- Per-turn live events (`BattleTurnEvent`) are published in MP-07 with throttling; the contract exists already.
- `IGameStats` port and adapter: MP-08.

## Assumptions added in MP-03 (to surface in README)
- SUP-11: attack and hit points in [1, 10,000], defense in [0, 10,000]; bounds the turn count and the report size.
- SUP-12: `MaxTurns` guard (default 100,000) returns an invariant error instead of throwing; the worker dead-letters it.
- SUP-13: dodge formula `defense / (defense + attack)` in basis points, capped at 7,500 (refines SUP-01).
- SUP-14: loot percentage is drawn once per battle **after** the fight, so it never alters the turn sequence.

## Backlog / ideas (not scheduled)
- Multi-target Domain to `netstandard2.1` for Unity (ADR-11).
- Stream partitioning for N workers (ADR-06).
- Rebuild-leaderboard runbook script (RB-05).

## Open questions for the user
- None right now.
