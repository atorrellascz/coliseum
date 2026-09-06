# Argo CD manifests

| File | Purpose |
|------|---------|
| `project.yaml` | `AppProject coliseum`: one source repository (this repo, plus the released OCI chart), one destination namespace, a whitelist of namespaced resource kinds, nothing cluster-scoped. |
| `application.yaml` | `Application coliseum`: chart path `deploy/helm/coliseum` with `values-local.yaml`, automated sync with prune and self-heal, server-side apply, retry with backoff, HPA-owned replica counts ignored. |

Sync waves (annotations in the chart): redis (0) → worker (1) → api (2) → mcp (3). The API canary
(`Rollout` + `AnalysisTemplate`) renders when `api.rollout.enabled=true`.

Install on a local cluster with `scripts/argocd-up.sh` (Argo CD stable, server-side apply, waits for
`Synced/Healthy`, prints the UI URL and the admin password). Design and validation status: `docs/gitops.md`.
