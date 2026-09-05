# Coliseum

[![ci](https://github.com/atorrellascz/coliseum/actions/workflows/ci.yml/badge.svg)](https://github.com/atorrellascz/coliseum/actions/workflows/ci.yml)

A backend service and battle engine for the "Backend Development Hands-on Test": players register, challenge each
other, a worker simulates the battles deterministically, settles the loot atomically in Redis, and ranks players
by everything they have stolen. Live events reach browsers over SignalR, AI agents play through an MCP server, and
the whole thing ships as containers with a Helm chart, dashboards, alerts and runbooks.

**Release:** [v1.0.0](https://github.com/atorrellascz/coliseum/releases/tag/v1.0.0): images `ghcr.io/atorrellascz/coliseum-{api,worker,mcp}:1.0.0`, NuGet `Coliseum.Domain` / `Coliseum.Contracts` 1.0.0, Helm chart `oci://ghcr.io/atorrellascz/charts/coliseum`.

**Stack:** C# / .NET 10 · Redis 7 (store, queue, leaderboard, pub/sub, Lua) · ASP.NET Core Minimal APIs · SignalR ·
OpenTelemetry · Model Context Protocol · Docker Compose · Helm / k3d · GitHub Actions · Terraform (AWS) · Argo CD.

## 5-minute quick start

```bash
git clone https://github.com/atorrellascz/coliseum.git && cd coliseum
docker compose -f deploy/compose/docker-compose.yml up --build -d     # Redis, API, worker, MCP, Grafana
API_URL=http://localhost:8080 API_KEY=dev-service-key bash scripts/smoke.sh
```

`smoke.sh` gets a token, creates three players, queues five battles, waits for the worker to settle them, and checks
that the leaderboard total equals the loot produced. Then open:

| What | Where |
|------|-------|
| Arena (two tabs = two players fighting each other automatically) | http://localhost:8080/arena/?name=Ata&auto=1 and http://localhost:8080/arena/?name=Bot&auto=1 |
| API reference (Scalar / OpenAPI) | http://localhost:8080/scalar |
| Grafana: API RED, queue USE, game economy (admin / admin) | http://localhost:3000 |
| MCP server for AI agents (`X-Api-Key: dev-mcp-key`) | http://localhost:8082/mcp |
| Health and metrics | http://localhost:8080/healthz/ready · http://localhost:8081/metrics |

The complete list of things to try is in [What you can try](#what-you-can-try).

## Walkthrough with curl

```bash
API=http://localhost:8080
TOKEN=$(curl -s -X POST $API/auth/token -H "X-Api-Key: dev-service-key" | sed -E 's/.*"accessToken":"([^"]+)".*/\1/')

# create two players (service token); each answer carries a player-scoped token
curl -s -X POST $API/players -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"name":"Ata","description":"the first","gold":500,"silver":120,"attack":70,"defense":30,"hitPoints":100}'
curl -s -X POST $API/players -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"name":"Bot","description":"","gold":500,"silver":120,"attack":60,"defense":40,"hitPoints":120}'

# queue a battle (202 Accepted) and read the report once the worker settled it
curl -s -X POST $API/battles -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"attackerId":"<ata id>","defenderId":"<bot id>"}'
curl -s $API/battles/<battle id> -H "Authorization: Bearer $TOKEN"
curl -s "$API/leaderboard?limit=10" -H "Authorization: Bearer $TOKEN"
```

Full endpoint reference with responses: [docs/api.md](docs/api.md).

## How it is built

```
                +----------------+        HTTP (bearer)        +----------------+
  AI agents --> |  Coliseum.Mcp  | --------------------------> |  Coliseum.Api  | <---- players / arena client (REST + SignalR)
                +----------------+                             +-------+--------+
                                                                       | create player (Lua, SET NX)
                                                                       | submit: HSET battle:{id} queued, then XADD
                                                                       | read: HGETALL battle, ZREVRANGE leaderboard
                                                                       v
                                                              +-----------------+
                                                              |      Redis      |  hashes · sorted set · streams · pub/sub · Lua
                                                              +--------+--------+
                                                                       ^  XREADGROUP / XAUTOCLAIM / XACK, EVALSHA apply_battle, PUBLISH events
                                                              +--------+--------+
                                                              | Coliseum.Worker |  BattleScheduler + BattleEngine
                                                              +-----------------+
```

| Project | Responsibility |
|---------|----------------|
| `Coliseum.Domain` | Value objects, `Player` aggregate, deterministic PRNG, **`BattleEngine`**. Zero dependencies; packaged as NuGet. |
| `Coliseum.Contracts` | DTOs and live events. Zero dependencies; packaged as NuGet. |
| `Coliseum.Application` | Ports, use cases, **`BattleScheduler`**, telemetry instruments. |
| `Coliseum.Infrastructure.Redis` | One adapter per port, Lua scripts (`create_player`, **`apply_battle`**, `mark_battle`), key schema, health. |
| `Coliseum.ServiceDefaults` | OpenTelemetry, Prometheus, health endpoints, logging. |
| `Coliseum.Api` / `Worker` / `Mcp` | Composition roots (`Program.cs` is five lines; an architecture test keeps it so). |

Dependencies point inward and a test fails the build if they do not. The design document is
[docs/architecture.md](docs/architecture.md).

## Key design decisions

| Decision | Why | ADR |
|----------|-----|-----|
| Redis Streams + consumer group as the queue | order, pending list, reclaim, dead-letter, no extra system | [0002](docs/adr/0002-redis-streams-queue.md) |
| Settlement in one atomic, idempotent Lua script keyed by battle id | at-least-once delivery + idempotent effect = exactly-once outcome, without locks | [0003](docs/adr/0003-idempotent-settlement-lua.md) |
| Deterministic engine (xoshiro256** seeded by the battle id, integer math) | safe reprocessing, replays, golden tests, cross-platform equality | [0004](docs/adr/0004-deterministic-rng.md), [0007](docs/adr/0007-integer-arithmetic.md) |
| In-memory single-writer scheduler in the worker | parallel battles without shared players, no overtaking per player, no distributed locks | [0005](docs/adr/0005-in-memory-scheduler.md) |
| One worker replica = strict global order; partition the stream to scale | honest about the multi-replica case | [0006](docs/adr/0006-single-worker-ordering.md) |
| JWT HS256 + API-key exchange behind a port; data-dependent rules in use cases | no IdP in the exercise; swapping one is one adapter | [0008](docs/adr/0008-jwt-hs256-behind-port.md), [0015](docs/adr/0015-authorization-in-use-cases.md) |
| Live events: worker → Redis pub/sub → relay on every API replica → SignalR | no backplane needed, worker never knows SignalR | [0010](docs/adr/0010-signalr-redis-backplane.md) |
| MCP server as a client of the API + local simulations with the Domain package | agents get the same rules as humans; what-if without side effects | [0013](docs/adr/0013-mcp-server.md) |
| Chiseled, non-root, read-only containers; Helm with NetworkPolicies and SLO alerts | operable from day one | [0012](docs/adr/0012-helm-argocd-rollouts.md) |

All sixteen ADRs: [docs/adr/](docs/adr/).

## Battle rules as implemented

- The initiator attacks first; roles alternate every turn.
- Attack decays with health: `max(ceil(base × 50 %), floor(base × hp / maxHp))`. Spec example: 70 attack at 90 hp
  of 100 → 63; never below 35.
- Dodge chance = `defense / (defense + attack)` in basis points, capped at 75 %; a roll in [0, 10,000) below it misses.
- The battle ends when a fighter reaches 0 hp (a 100,000-turn guard fails the battle instead of looping).
- The winner steals one percentage in [5, 10] of each resource, rounded up per resource. Spec example: 500 gold and
  120 silver at 7 % → 35 and 9. The score added to the leaderboard is gold + silver stolen.
- Balance observation: with identical stats the attacker wins ~72 % of simulations (first strike). It is measured,
  documented and available to agents through `estimate_win_chance`.

## Assumptions and trade-offs

| ID | Assumption |
|----|------------|
| SUP-01 | Dodge formula `def / (def + atk)`, capped at 75 % so every battle terminates |
| SUP-02 | xoshiro256** seeded from the battle id; `System.Random` is never used |
| SUP-03 | One loot percentage per battle, applied per resource with ceil |
| SUP-04 | Loot is recomputed on the loser's live balance at settlement time |
| SUP-05 | Excess above 1e9 on the winner is burned |
| SUP-06 | Names are unique ignoring case and surrounding spaces |
| SUP-07 | One worker gives strict global order; scale by partitioning the stream |
| SUP-08 | Players are not deleted in v1; a missing player at processing time fails the battle |
| SUP-09 | HS256 with a shared secret; a corporate IdP is a one-adapter swap |
| SUP-10 | Redis runs with `noeviction` and AOF |
| SUP-11 | Stats capped at 10,000 to bound turns and report size |
| SUP-12 | The turn guard returns an error instead of throwing; the worker dead-letters it |
| SUP-14 | The loot percentage is drawn after the fight so it never changes the turn sequence |
| SUP-15 | Stream reads poll every 250 ms when idle (StackExchange.Redis has no blocking reads) |

Trade-offs made on purpose: polling instead of `XREADGROUP BLOCK` (ADR-0016); a single Redis instance instead of
a cluster; HS256 instead of an IdP; turn events only for battles up to 100 turns; the arena client has no build step
and loads SignalR from a CDN.

## What was left out, and why

- **Redis Cluster / multi-region**: the settlement script touches keys of two players and the leaderboard; a cluster
  needs hash tags or a different settlement split. Documented in ADR-0001.
- **Horizontal worker scaling**: needs stream partitioning (ADR-0006). One replica is correct and honest for the
  exercise.
- **Identity provider, token revocation, anti-cheat**: see [docs/security.md](docs/security.md).
- **Applying the Terraform**: validated only; an EKS cluster costs real money.

## What I would do next

1. Partition the battle stream by attacker and run N workers.
2. Replace HS256 with the company IdP (JWKS) and add per-player API keys.
3. Snapshot the economy to an analytical store and add a balance dashboard for game design.
4. Redis Cluster with hash tags, or a settlement split per player shard.
5. Load test with k6 at 5,000 battles and publish the numbers.

## Quality gates

- Build with warnings as errors, analyzers at `latest-recommended`, `dotnet format` enforced.
- 155 tests: unit (engine rules, spec examples, 2,000-battle property test, scheduler simulation, use cases with
  fakes, architecture rules), golden regression reports, integration against a real Redis (Testcontainers), API via
  `WebApplicationFactory`, API + worker end to end, SignalR end to end.
- CI on every push: build, format, tests, integration with a Redis service container, three container images with a
  Trivy gate, Helm lint + kubeconform. Release on tags: GHCR images with SBOM, NuGet packages, Helm OCI chart.

## Running from source

```bash
docker run -d --name redis -p 6379:6379 redis:7-alpine --appendonly yes --maxmemory-policy noeviction
dotnet build Coliseum.slnx
dotnet test --project tests/Coliseum.UnitTests
dotnet test --project tests/Coliseum.RegressionTests
dotnet test --project tests/Coliseum.IntegrationTests      # needs Docker (Testcontainers) or REDIS_URL

ASPNETCORE_URLS=http://localhost:5080 ASPNETCORE_ENVIRONMENT=Development dotnet run --project src/Coliseum.Api
ASPNETCORE_URLS=http://localhost:5081 ASPNETCORE_ENVIRONMENT=Development dotnet run --project src/Coliseum.Worker
ASPNETCORE_URLS=http://localhost:5082 ASPNETCORE_ENVIRONMENT=Development Mcp__ApiBaseUrl=http://localhost:5080 dotnet run --project src/Coliseum.Mcp
```

Development settings ship a dev signing key and API key (`dev-service-key`, `dev-mcp-key`); production values come
from the environment or Kubernetes Secrets.

## What you can try

1. **Full stack in one command**
   `docker compose -f deploy/compose/docker-compose.yml up --build -d`, then
   `API_URL=http://localhost:8080 API_KEY=dev-service-key bash scripts/smoke.sh`.
2. **Watch battles live**: open http://localhost:8080/arena/?name=Ata&auto=1 and
   http://localhost:8080/arena/?name=Bot&auto=1 in two tabs. Each tab creates its player and challenges random
   opponents every few seconds; HP bars animate turn by turn, loot and the leaderboard update from live events.
   Change `interval=3000` or add more tabs with other names.
3. **Operate it**: the back-office at http://localhost:8080/backoffice/ (sign in with `dev-service-key`): API RED, queue USE,
   economy with the attacker win rate against the 72 % baseline, and a live battle feed with full reports. Grafana at http://localhost:3000 (admin / admin), dashboard "Coliseum": request rate, 5xx ratio,
   latency percentiles, submitted vs processed, submission-to-settlement p95, queue length / pending / DLQ,
   resources stolen, turns per battle.
4. **Talk to the API**: http://localhost:8080/scalar, or the curl walkthrough above.
5. **Embed it**: http://localhost:8080/widget/ shows the one-script-tag widget (live leaderboard + battle feed) with a
   player token; security notes in [docs/widget.md](docs/widget.md). Spectate a player: http://localhost:8080/arena/?watch=<playerId>.
6. **Let an agent play**: point an MCP client at http://localhost:8082/mcp with header `X-Api-Key: dev-mcp-key`
   (stdio variant for Claude Desktop in [docs/mcp.md](docs/mcp.md)). Tools: create players, submit and wait for
   battles, read reports and the leaderboard, simulate battles and estimate win chances locally.
7. **Kubernetes**: `KUBE_CONTEXT=docker-desktop bash scripts/helm-up.sh` (builds images, installs the chart, runs
   the smoke test through a port-forward) or `bash scripts/k3d-up.sh` for a 3-node k3d cluster.
8. **Break it and watch it recover**: kill the worker container mid-run (`docker compose kill worker` then
   `docker compose up -d worker`); pending battles are reclaimed and settled exactly once, `duplicate` never
   changes a balance.

## Documentation map

| Document | Content |
|----------|---------|
| [docs/architecture.md](docs/architecture.md) | Software design document |
| [docs/redis-data-model.md](docs/redis-data-model.md) | Keys, scripts, operational settings |
| [docs/api.md](docs/api.md) · [docs/live-events.md](docs/live-events.md) · [docs/mcp.md](docs/mcp.md) | Interfaces |
| [docs/security.md](docs/security.md) · [docs/sre.md](docs/sre.md) · [docs/runbooks/](docs/runbooks/) | Operating it |
| [docs/deploy.md](docs/deploy.md) · [docs/local-kubernetes.md](docs/local-kubernetes.md) · [docs/ci.md](docs/ci.md) · [docs/gitops.md](docs/gitops.md) · [deploy/terraform/README.md](deploy/terraform/README.md) | Shipping it |
| [docs/adr/](docs/adr/) · [docs/TASKS.md](docs/TASKS.md) · [docs/DEVLOG.md](docs/DEVLOG.md) | Decisions and history |
| [AGENTS.md](AGENTS.md) | How AI was used |
