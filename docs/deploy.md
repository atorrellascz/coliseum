# Deployment

Three ways to run Coliseum, from a laptop to a cluster. Every path ends with `scripts/smoke.sh`.

## 1. Docker Compose (recommended for a first look)

```bash
docker compose -f deploy/compose/docker-compose.yml up --build -d
API_URL=http://localhost:8080 API_KEY=dev-service-key bash scripts/smoke.sh
```

| Service | URL | Notes |
|---------|-----|-------|
| API | http://localhost:8080 (`/scalar` for docs) | JWT + API key; defaults are compose-only secrets |
| Worker | http://localhost:8081/healthz/ready, `/metrics` | one replica keeps global order |
| MCP | http://localhost:8082/mcp | header `X-Api-Key: dev-mcp-key` |
| Grafana | http://localhost:3000 (admin / admin) | dashboard "Coliseum": RED, queue USE, economy |
| Redis | localhost:6379 | AOF on, `noeviction` |

Override secrets with environment variables (`COLISEUM_SIGNING_KEY`, `COLISEUM_API_KEY`, `COLISEUM_MCP_CLIENT_KEY`)
or a `.env` file next to the compose file. Containers run read-only, non-root, on chiseled Ubuntu images
(no shell, no package manager).

## 2. Images

```bash
docker build --target api    -t coliseum-api:local    .
docker build --target worker -t coliseum-worker:local .
docker build --target mcp    -t coliseum-mcp:local    .
```

One multi-stage `Dockerfile`: a cached restore layer (only project files), `dotnet publish` per host, and
`mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled` at runtime. Configuration is environment-only
(`Redis__ConnectionString` or `REDIS_URL`, `Auth__SigningKey`, `Auth__ApiKeys__0`, `Mcp__*`, `OTEL_EXPORTER_OTLP_ENDPOINT`).

## 3. Kubernetes with Helm

```bash
helm lint deploy/helm/coliseum
KUBE_CONTEXT=docker-desktop bash scripts/helm-up.sh      # builds images, installs, port-forwards, runs the smoke test
```

The chart (`deploy/helm/coliseum`) renders:

| Resource | Purpose |
|----------|---------|
| Deployment `api` + Service + PDB + HPA (CPU 70 %) | stateless API, rolling updates with zero unavailable |
| Deployment `worker` (Recreate, 1 replica, grace 45 s) | single consumer, in-flight battles settle before exit |
| Deployment `mcp` + Service | MCP server pointing at the API service |
| StatefulSet `redis` + headless Service + PVC | embedded Redis for dev; `redis.embedded=false` + `redis.external.url` for ElastiCache |
| Secret | signing key, API key, MCP client key (`secrets.existingSecret` for External Secrets) |
| NetworkPolicy ×2 | only api/worker may reach Redis; hosts accept only port 8080 |
| ServiceMonitor + PrometheusRule (opt-in) | scraping and SLO alerts (error budget burn, stalled queue, DLQ growth, latency) |

Values files: `values.yaml` (production-shaped defaults), `values-local.yaml` (Docker Desktop / k3d: local images,
NodePort, no HPA). Scripts always pass `--context` explicitly so the globally selected kubectl context is never used
by accident.

### k3d

`bash scripts/k3d-up.sh` creates a 1-server / 2-agent cluster, imports the local images and runs `helm-up.sh`.

## 4. Scripts for demos and operations

| Script | Purpose |
|--------|---------|
| `scripts/run.ps1 <script.sh>` | PowerShell launcher that runs a script with Git Bash (a plain `bash` is WSL when WSL is installed, and has no k3d/helm/kubectl) |
| `scripts/smoke.sh` / `scripts/smoke.ps1` | end-to-end check through the API (token, players, battles, leaderboard accounting) |
| `scripts/helm-up.sh` | build images, install the chart, `port-forward.sh`, smoke |
| `scripts/port-forward.sh` | forward API 8080, MCP 8082, Grafana 3000 (the Compose ports: one stack at a time); refuses to start while Compose is up |
| `scripts/k3d-up.sh` | 3-node k3d cluster + image import + `helm-up.sh` |
| `scripts/k3d-import.sh` | import images into k3d through a single-platform tarball (works with Docker's containerd image store) |
| `scripts/argocd-up.sh` | install Argo CD, apply AppProject + Application, wait for Synced/Healthy, port-forward the UI |
| `scripts/chaos-worker.sh` | dead-consumer demo: XPENDING → XAUTOCLAIM → exactly-once settlement (Compose or `MODE=k8s`) |
| `scripts/mcp-demo.sh` | MCP walkthrough over HTTP (initialize, tools/list, simulate, create players, play, leaderboard) |
| `scripts/replay-dlq.sh` | move dead-lettered battles back to the stream (RB-02) |

The local chart values enable `monitoring.otelLgtm` (Grafana + Prometheus + Loki + Tempo with the Coliseum dashboard) so the
Kubernetes track has the same dashboards as Compose. See `docs/demo-playbook.md` for the ordered rehearsal.

## 5. What is not built (and why)

- **Redis Cluster / multi-region**: the exercise runs on one Redis; the key schema uses no hash tags yet.
- **Argo CD / Rollouts** (MP-11): manifests are planned; the chart is the unit Argo would sync.
- **Terraform** (MP-01): AWS VPC / EKS / ECR / ElastiCache definitions, validated but not applied (cost).
