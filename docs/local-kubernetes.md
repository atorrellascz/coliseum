# Local Kubernetes: Docker Desktop and k3d

Both paths use the same chart and the same script (`scripts/helm-up.sh`), which always passes `--context`
explicitly. Measured on a Windows 11 laptop (16 logical cores, 32 GB) on 2026-09-05.

## Docker Desktop (measured)

| Step | Time |
|------|------|
| `docker compose up --build` from a clean image cache (three chiseled images built) | 266 s |
| `docker build` x3 with a warm cache | ~5 s |
| `helm upgrade --install --wait` (4 pods: api, worker, mcp, redis with PVC) | ~25 s |
| Whole `scripts/helm-up.sh` (builds, install, port-forward, smoke test) | 49 s |
| Hosts ready after container start | ~3 s |

Result: 4/4 pods Running, `scripts/smoke.sh` green through the port-forward, `helm lint` clean.
Images: api 193 MB, worker 187 MB, mcp 191 MB (chiseled Ubuntu, non-root, no shell).

Notes
- Docker Desktop's Kubernetes shares the Docker image store, so `image.pullPolicy: Never` works with no registry.
- The cluster is single-node: `PodDisruptionBudget` and anti-affinity cannot be exercised here.
- Existing workloads on the same node (other projects) compete for CPU; scale them down first.

## k3d (script ready, not measured on this machine)

`scripts/k3d-up.sh` creates `k3d-coliseum` (1 server, 2 agents), imports the local images with `k3d image import`
and calls `helm-up.sh` with `KUBE_CONTEXT=k3d-coliseum`. k3d was not installed on the development machine, so no
numbers are claimed. What k3d adds over Docker Desktop:

| Concern | Docker Desktop | k3d |
|---------|----------------|-----|
| Nodes | 1 | N (multi-node scheduling, PDB and anti-affinity testable) |
| Create / reset | toggle in settings (minutes) | `k3d cluster create` (tens of seconds), disposable per branch |
| Images | shared daemon, no import | `k3d image import` or a local registry |
| CI | not available | runs in GitHub Actions |
| Ingress | none by default | Traefik bundled |

Recommendation: Docker Desktop for a quick local check; k3d for CI and for anything that needs more than one node.
