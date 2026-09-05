# Runbooks

| Runbook | Trigger |
|---------|---------|
| [RB-01 Backlog or old pending entries](RB-01-backlog.md) | pending grows, latency alert |
| [RB-02 Worker crash loop or stuck loop](RB-02-worker-crashloop.md) | restarts, readiness, stalled queue |
| [RB-03 Redis degraded](RB-03-redis-degraded.md) | readiness on `redis`, slow PING |
| [RB-04 Duplicates rising](RB-04-duplicates.md) | `result="duplicate"` counter |
| [RB-05 Leaderboard mismatch](RB-05-leaderboard-mismatch.md) | support ticket |
| [RB-06 Rollout stuck](RB-06-rollout-stuck.md) | Argo Rollouts paused / analysis inconclusive |

SLOs and alert definitions: `docs/sre.md`; alert rules ship in the Helm chart (`monitoring.serviceMonitor=true`).
