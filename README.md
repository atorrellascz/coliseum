# Coliseum

Backend service + battle engine for the "Backend Development Hands-on Test".

> Status: **MP-06b done**: the full spec runs end to end (API, worker, Redis, MCP server; 146 tests green, smoke script OK).
> Next: Docker Compose + Helm (MP-09), live events (MP-07), back-office (MP-08).
> Progress board: `docs/TASKS.md`. Step-by-step log: `docs/DEVLOG.md`.

## Stack

C# / .NET 10 · Redis 7 (primary store, queue, leaderboard, pub/sub) · ASP.NET Core Minimal APIs · SignalR ·
OpenTelemetry · Model Context Protocol (MCP) server · Docker Compose · Helm / k3d · Argo CD · Terraform (AWS) · GitHub Actions.

## Solution layout

| Project | Role | Depends on |
|---------|------|-----------|
| `src/Coliseum.Domain` | Entities, value objects, **battle engine**, deterministic RNG. Zero dependencies. Packed as NuGet. | – |
| `src/Coliseum.Contracts` | Request/response DTOs and live events. Packed as NuGet. | – |
| `src/Coliseum.Application` | Use cases, ports (interfaces), in-memory `BattleScheduler`, telemetry instruments. | Domain, Contracts |
| `src/Coliseum.Infrastructure.Redis` | One adapter per port, embedded Lua scripts, key schema, health check. | Application |
| `src/Coliseum.ServiceDefaults` | OpenTelemetry, health endpoints, JSON logging shared by all hosts. | – |
| `src/Coliseum.Api` | HTTP host: auth, endpoints, SignalR hub, static clients. | Application, Infrastructure, ServiceDefaults, Contracts |
| `src/Coliseum.Worker` | Battle processor: stream consumer + scheduler. | Application, Infrastructure, ServiceDefaults |
| `src/Coliseum.Mcp` | MCP server exposing the game as tools for AI agents. | Domain, Contracts, ServiceDefaults |
| `tests/Coliseum.UnitTests` | Domain + application + architecture rules, no I/O. | Domain, Application |
| `tests/Coliseum.RegressionTests` | Golden battle reports by seed. | Domain |
| `tests/Coliseum.IntegrationTests` | Real Redis (Testcontainers), API via `WebApplicationFactory`. | Infrastructure, Api, Worker |

Dependency rule: arrows point inward (hosts → infrastructure → application → domain). An architecture test enforces it.

## Quick start

```bash
docker run -d --name redis -p 6379:6379 redis:7-alpine --appendonly yes --maxmemory-policy noeviction
dotnet build Coliseum.slnx
dotnet test --project tests/Coliseum.UnitTests
dotnet test --project tests/Coliseum.RegressionTests
dotnet test --project tests/Coliseum.IntegrationTests      # needs Docker (Testcontainers) or REDIS_URL

# three terminals (Development settings ship a dev signing key and API key)
ASPNETCORE_URLS=http://localhost:8080 ASPNETCORE_ENVIRONMENT=Development dotnet run --project src/Coliseum.Api
ASPNETCORE_URLS=http://localhost:8081 ASPNETCORE_ENVIRONMENT=Development dotnet run --project src/Coliseum.Worker
ASPNETCORE_URLS=http://localhost:8082 ASPNETCORE_ENVIRONMENT=Development Mcp__ApiBaseUrl=http://localhost:8080 dotnet run --project src/Coliseum.Mcp

API_URL=http://localhost:8080 API_KEY=dev-service-key bash scripts/smoke.sh
```

Interactive API docs: http://localhost:8080/scalar. Full stack with Compose and Grafana arrives in MP-09.

## Documentation

- `docs/architecture.md` — software design document (projects, domain, Redis model, queue guarantees, concurrency, security, observability, assumptions)
- `docs/redis-data-model.md` — every key, script and operational setting
- `docs/api.md` — endpoints with curl examples
- `docs/mcp.md` — MCP tools, transports and client configuration
- `docs/TASKS.md` — progress board
- `docs/adr/` — architecture decision records
- `docs/DEVLOG.md` — build log, one entry per step
- `docs/runbooks/` — SRE runbooks
- `AGENTS.md` — how AI was used in this repository
