# GitOps with Argo CD and Argo Rollouts

The Helm chart is the deployable unit; Argo CD keeps a cluster converged to it (ADR-0012).

## Manifests

| File | Purpose |
|------|---------|
| `deploy/argocd/project.yaml` | `AppProject coliseum`: one source repo (and the released OCI chart), one destination namespace, an explicit whitelist of resource kinds, nothing cluster-scoped |
| `deploy/argocd/application.yaml` | `Application coliseum`: chart path in this repo, automated sync with prune and self-heal, server-side apply, retry with backoff, HPA-owned replicas ignored |
| `deploy/helm/coliseum/templates/rollout.yaml` | `Rollout` + `AnalysisTemplate` for the API, rendered only when `api.rollout.enabled=true` |

Sync waves order the first deploy and every change: `redis` (0) → `worker` (1) → `api` (2) → `mcp` (3).

## Canary for the API

With `api.rollout.enabled=true` the chart renders a `Rollout` instead of the `Deployment` (the HPA re-targets
automatically): 20 % → pause → 50 % → pause → 100 %. From step 1 an `AnalysisRun` queries Prometheus every minute,
five times, and fails the rollout when the 5xx ratio ≥ 1 % or p99 latency ≥ 250 ms. Abort routes all traffic back
to the stable ReplicaSet; RB-06 covers the operator side.

The worker stays a plain `Deployment` with `Recreate`: a canary of a single-consumer worker would only double the
consumers (ADR-0006).

## Install on a local cluster (k3d or Docker Desktop)

```bash
K="kubectl --context k3d-coliseum"           # or --context docker-desktop; never rely on the global context

# Argo CD
$K create namespace argocd
$K apply -n argocd -f https://raw.githubusercontent.com/argoproj/argo-cd/stable/manifests/install.yaml
$K -n argocd rollout status deploy/argocd-server

# Argo Rollouts (only needed when api.rollout.enabled=true)
$K create namespace argo-rollouts
$K apply -n argo-rollouts -f https://github.com/argoproj/argo-rollouts/releases/latest/download/install.yaml

# Coliseum
$K apply -f deploy/argocd/project.yaml
$K apply -f deploy/argocd/application.yaml
$K -n argocd get application coliseum -w        # Synced / Healthy once the four pods are ready

# UI
$K -n argocd port-forward svc/argocd-server 8443:443 &
$K -n argocd get secret argocd-initial-admin-secret -o jsonpath='{.data.password}' | base64 -d
```

Local images: `application.yaml` uses `values-local.yaml` (`pullPolicy: Never`), so build and import the images
first (`scripts/k3d-up.sh` does both and can be run before pointing Argo at the cluster).

## Validation status

- `helm lint` and `helm template` pass with `values.yaml`, `values-local.yaml` and `values-dev.yaml`; CI runs
  `kubeconform -strict -ignore-missing-schemas` on the rendered output (Rollout / AnalysisTemplate / ServiceMonitor
  are CRDs without upstream JSON schemas, hence the flag).
- The Argo CD manifests are validated structurally (`kubectl --dry-run=client` requires the CRDs; not part of CI).
- A live Argo CD installation was not exercised in this repository: the steps above are the documented path, and
  the chart they sync is the one already verified on Docker Desktop and k3d (docs/local-kubernetes.md).
