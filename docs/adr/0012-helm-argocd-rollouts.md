# ADR-0012: Helm chart, Argo CD for GitOps, Argo Rollouts for the API canary

- Status: Accepted (Helm implemented in MP-09; Argo manifests in MP-11)
- Date: 2026-09-03

## Context and decision
The target platform is Kubernetes. A Helm chart is the deployable unit: it renders the API (Deployment or
Rollout), the worker, the MCP server, an embedded or external Redis, secrets, network policies and monitoring
objects. Argo CD keeps the cluster converged to the chart in Git (automated sync, prune, self-heal); Argo Rollouts
promotes the API canary only when Prometheus says the error ratio and latency are within budget.

## Consequences
- One chart, three value files (`values.yaml`, `values-local.yaml`, `values-dev.yaml`): the same artifact runs on
  Docker Desktop, k3d and EKS.
- The worker stays a plain Deployment with `Recreate`: a canary of a single-consumer worker would only double
  consumers (ADR-0006).
- CRD-dependent objects (ServiceMonitor, PrometheusRule, Rollout, AnalysisTemplate) are opt-in flags so the chart
  installs on a bare cluster.
- `helm lint` and `kubeconform` run in CI; the release workflow pushes the chart as an OCI artifact, which is what
  Argo CD points at in production.
