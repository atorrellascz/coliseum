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
$K apply --server-side --force-conflicts -n argocd -f https://raw.githubusercontent.com/argoproj/argo-cd/stable/manifests/install.yaml   # server-side: the ApplicationSet CRD is too big for client-side apply
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
- Exercised live on k3d (2026-09-05) with `scripts/argocd-up.sh`: Argo CD stable installed with server-side apply
  (the ApplicationSet CRD exceeds the client-side annotation limit), AppProject + Application applied, the
  application synced from GitHub `main` and reached **Healthy** with all five workloads created by Argo; deleting
  the MCP Deployment was repaired by self-heal (Argo notices within its refresh interval, up to 3 minutes, or at
  once after a Refresh in the UI). A permanent OutOfSync on the Redis StatefulSet was traced to
  `volumeClaimTemplates` lacking `apiVersion`/`kind` and fixed in the chart.
- Argo Rollouts (the canary) was validated by rendering and kubeconform only; installing the Rollouts controller
  and promoting a canary live is the next step.
