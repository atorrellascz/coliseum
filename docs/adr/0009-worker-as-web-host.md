# ADR-0009: The worker is an ASP.NET Core host

- Status: Accepted
- Date: 2026-09-03

## Context and decision
The worker has no HTTP API, but Kubernetes needs liveness/readiness probes and Prometheus needs a scrape target.
Using the Web SDK with a single `BackgroundService` costs one extra assembly and gives `/healthz/live`,
`/healthz/ready` (Redis PING plus a loop heartbeat) and `/metrics` through the shared `ServiceDefaults`.

## Consequences
- The three hosts share exactly the same operational surface and the same `HostingExtensions` shape; probes in
  the Helm chart are identical for api, worker and mcp.
- `WorkerHealthCheck` reports unhealthy when the processing loop has not beaten for 30 s, which is the "worker
  stuck" signal used by the runbooks.
- Graceful shutdown uses the host's `SIGTERM` handling: the loop stops reading, in-flight battles get up to 30 s,
  Kubernetes grace is set to 45 s.
