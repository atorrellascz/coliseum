# Coliseum

Backend service + battle engine for the "Backend Development Hands-on Test".

> Status: **MP-03 done** (domain + battle engine, 82 tests green). Hosts arrive in MP-06.
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
dotnet build Coliseum.slnx
dotnet test  Coliseum.slnx
# full stack (Redis + API + Worker + MCP + Grafana): see deploy/compose (MP-09)
```

## Documentation

- `docs/adr/` — architecture decision records
- `docs/DEVLOG.md` — build log, one entry per step
- `docs/runbooks/` — SRE runbooks
- `AGENTS.md` — how AI was used in this repository
