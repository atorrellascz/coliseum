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
| MP-05 | Redis adapters, Lua scripts, integration tests (Testcontainers) | [x] code done, review pending | see DEVLOG |
| MP-06 | API + Worker hosts, ServiceDefaults, auth, smoke script | [x] code done, review pending | see DEVLOG |
| MP-06b | MCP server (tools over the API + local simulation) | [x] code done, review pending | see DEVLOG |
| MP-07 | SignalR hub + arena auto-play client | [x] code done, review pending | see DEVLOG |
| MP-08 | Back-office (RED / USE / economy) + admin stats | [ ] | |
| MP-09 | Dockerfile, Compose (+ Grafana), Helm, k3d comparison | [x] code done, review pending; Compose, docker-desktop and k3d all verified | see DEVLOG |
| MP-10 | GitHub Actions CI + release | [x] CI green on GitHub (run 33928977302) | 41b62e7, 2cc0cdf |
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

## MP-05 checklist (review one by one)

`src/Coliseum.Infrastructure.Redis`
- [x] Connection/RedisOptions.cs, RedisConnectionFactory.cs — one multiplexer, AbortOnConnectFail=false
- [x] Keys/RedisKeys.cs — the whole key schema
- [x] Scripts/LuaScripts.cs + create_player.lua, apply_battle.lua, mark_battle.lua
- [x] Serialization/ColiseumJsonContext.cs — source-generated JSON, ids as plain strings
- [x] Adapters/RedisPlayerRepository.cs, RedisBattleQueue.cs, RedisBattleLedger.cs, RedisBattleReportStore.cs, RedisLeaderboard.cs, RedisEventPublisher.cs, UlidIdGenerator.cs, SystemClock.cs
- [x] Health/RedisHealthCheck.cs, DependencyInjection/RedisServiceCollectionExtensions.cs
- [ ] Adapters/RedisGameStats.cs — stub until MP-08

`tests/Coliseum.IntegrationTests` (Testcontainers Redis 7, or `REDIS_URL`)
- [x] Fixtures/RedisFixture.cs — container per assembly, key prefix per test
- [x] Redis/PlayerRepositoryTests.cs, BattleQueueTests.cs, BattleLedgerTests.cs, LeaderboardTests.cs, BattleReportStoreTests.cs

## MP-06 checklist (review one by one)

- [x] ServiceDefaults/Extensions.cs, HealthEndpoints.cs — OTel, Prometheus, OTLP opt-in, health, REDIS_URL alias
- [x] Api/Program.cs, ApiAssemblyMarker.cs, appsettings*.json
- [x] Api/Auth/AuthPolicies.cs, HmacJwtTokenService.cs, ApiKeyExchange.cs
- [x] Api/Endpoints/PlayerEndpoints.cs, BattleEndpoints.cs, LeaderboardEndpoints.cs
- [x] Api/Middleware/ProblemDetailsMapping.cs, SecurityHeaders.cs; Api/Options/RateLimitOptions.cs
- [ ] Api/Hubs/* — MP-07; Api/Endpoints/AdminEndpoints — MP-08
- [x] Worker/Program.cs, WorkerAssemblyMarker.cs, appsettings*.json
- [x] Worker/Processing/BattleProcessorService.cs, BattleExecutor.cs, WorkerHealthCheck.cs; Worker/Options/WorkerOptions.cs
- [x] scripts/smoke.sh — curl only, no jq
- [x] IntegrationTests/Fixtures/ApiFactory.cs (+ WorkerFactory), Api/AuthTests.cs, PlayerEndpointTests.cs, BattleEndpointTests.cs, LeaderboardEndpointTests.cs, Worker/EndToEndTests.cs

## MP-06b checklist (review one by one)

- [x] Mcp/Program.cs — Streamable HTTP on /mcp or `--stdio`
- [x] Mcp/Options/McpOptions.cs, McpApiKeyGuard.cs, ServiceTokenProvider.cs, ColiseumApiClient.cs
- [x] Mcp/Tools/PlayerTools.cs, BattleTools.cs, LeaderboardTools.cs, SimulationTools.cs
- [x] docs/mcp.md

Verification for MP-05 / MP-06 / MP-06b DoD
- [x] `dotnet build Coliseum.slnx`: succeeded, 0 warnings; `dotnet format` clean on every project
- [x] UnitTests 103 / 103 · RegressionTests 10 / 10 · IntegrationTests 33 / 33 (real Redis via Testcontainers, API + worker end-to-end)
- [x] Live run: Redis container + API + Worker + MCP, `scripts/smoke.sh` OK, MCP initialize handshake OK (see DEVLOG)
- [ ] User reviewed every file above
- [x] Docs: architecture.md (SDD), redis-data-model.md, api.md, mcp.md, ADR-0016

## MP-09 checklist (review one by one)

- [x] Dockerfile — multi-stage, cached restore layer, three targets on `aspnet:10.0-noble-chiseled`, non-root
- [x] deploy/compose/docker-compose.yml — redis (AOF, noeviction), api, worker, mcp, grafana/otel-lgtm; read-only containers
- [x] deploy/compose/grafana/dashboards.yaml, coliseum.json — RED / USE / economy dashboard provisioned
- [x] deploy/helm/coliseum — Chart.yaml, values.yaml, values-local.yaml, templates: _helpers, api (Deployment/Service/PDB/HPA/Ingress), worker, mcp, redis (StatefulSet), secret, serviceaccount, networkpolicy, servicemonitor (+ PrometheusRule), NOTES
- [x] scripts/helm-up.sh, scripts/k3d-up.sh; Makefile targets docker-build / compose-up / helm-lint / helm-up
- [x] docs/deploy.md, docs/local-kubernetes.md

Verification for MP-09 DoD
- [x] `docker compose up --build` from scratch: 266 s; `scripts/smoke.sh` OK; MCP initialize 200; metrics in Prometheus via OTLP; dashboard provisioned
- [x] `helm lint` clean; `helm template` renders 12 resources
- [x] `KUBE_CONTEXT=docker-desktop bash scripts/helm-up.sh`: 4/4 pods Running, smoke OK, 49 s
- [x] k3d v5.9.0: `scripts/k3d-up.sh` → cluster in 116 s, helm install 49 s, 4/4 pods on 3 nodes, smoke OK (docs/local-kubernetes.md)
- [x] Program.cs of the three hosts reduced to composition-only scripts (`HostingExtensions`), guarded by `HostCompositionRulesTests`
- [ ] User reviewed every file above

## MP-07 checklist (review one by one)

- [x] Api/Hubs/ArenaHub.cs — auto-join own player group from the token; JoinBackOffice / WatchPlayer for service tokens
- [x] Api/Hubs/ArenaEventRelay.cs — Redis pub/sub → SignalR groups, raw JSON forwarded; no backplane (ADR-0010 rewritten)
- [x] Api/Auth/AuthPolicies.cs — JWT from `?access_token=` for `/hubs/*`; Api/Program.cs — AddSignalR, MapHub, relay hosted service
- [x] Application: `IPlayerRepository.ListRecentAsync`, `ListPlayersHandler` (+ `GET /players?limit=`), `ProcessBattleHandler` publishes turns (≤ 100), done, leaderboard snapshot
- [x] Infrastructure: `RedisPlayerRepository.ListRecentAsync` (players:index)
- [x] src/Coliseum.Api/wwwroot/arena/index.html, arena.js, arena.css — two windows = two players; auto-play; HP bars animated from turn events
- [x] Tests: ListPlayersHandlerTests, ProcessBattleHandler turn/leaderboard events, IntegrationTests/Api/ArenaHubTests (hub end to end through worker + Redis; anonymous rejected)
- [x] Package change: SignalR Redis backplane removed, SignalR.Client added for tests

Verification for MP-07 DoD
- [x] Solution build 0 warnings; UnitTests 107, RegressionTests 10, IntegrationTests 35
- [x] Hosts from source: `/arena/` served, smoke OK, hub negotiate 200 with token / 401 without, relay subscribed
- [ ] Two browser windows auto-playing for 5 minutes (manual check by the user; see docs/live-events.md)
- [ ] User reviewed every file above

## MP-10 checklist

- [x] .github/workflows/ci.yml — build + format + unit/regression; integration with a redis service container; images (matrix) + Trivy; helm lint + kubeconform
- [x] .github/workflows/release.yml — GHCR images with SBOM/provenance, NuGet packages to GitHub Packages, Helm chart as OCI
- [x] docs/ci.md; README badge
- [x] First real CI run green: build-test, integration (Redis service container), images ×3 + Trivy, helm + kubeconform (run 33928977302). A first attempt failed on a non-existent `trivy-action@0.28.0` tag; pinned to v0.36.0.

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
