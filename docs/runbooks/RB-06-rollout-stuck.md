# RB-06: API rollout paused or analysis inconclusive

**Trigger:** Argo Rollouts shows the API rollout `Paused` beyond the planned pause, or an `AnalysisRun` is
`Inconclusive` / `Failed`.

## Diagnose
1. `kubectl argo rollouts get rollout coliseum-coliseum-api -n coliseum --watch` — which step, which analysis.
2. `AnalysisRun` details: the two measurements are the 5xx ratio (< 1 %) and p99 latency (< 250 ms) from Prometheus.
   `Inconclusive` usually means no traffic during the window (metrics absent), not a bad build.
3. Compare canary vs stable in Grafana by pod label.

## Mitigate
- No traffic: generate some (`scripts/smoke.sh` against the ingress) and re-run, or `kubectl argo rollouts promote`
  if the change is low-risk.
- Real regression: `kubectl argo rollouts abort` (traffic returns to stable), then fix forward in Git; Argo CD will
  sync the new revision and start a new canary.

## Verify
Rollout `Healthy`, all pods on the new revision, error ratio and latency within SLO for 10 minutes.
