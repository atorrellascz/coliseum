# SRE: SLOs, alerts, runbooks

## Signals

One `Meter` and one `ActivitySource` named `coliseum` (`Application/Telemetry/ColiseumTelemetry.cs`), exported
through OpenTelemetry to Prometheus (`/metrics` on every host) and optionally OTLP. Ids are never metric tags.

| Metric (Prometheus name) | Type | Meaning |
|--------------------------|------|---------|
| `http_server_request_duration_seconds` (`http_route`, `http_response_status_code`) | histogram | RED of the API |
| `coliseum_battles_submitted_total` | counter | accepted battle requests |
| `coliseum_battles_processed_total{result}` | counter | processed, duplicate, player_missing, failed |
| `coliseum_battle_processing_duration_seconds` | histogram | load + simulate + settle |
| `coliseum_battle_queue_latency_seconds` | histogram | submission → settlement (user-facing) |
| `coliseum_battle_turns` | histogram | game balance |
| `coliseum_resources_stolen_total{resource}` | counter | economy |
| `coliseum_scheduler_running`, `coliseum_scheduler_pending` | gauge | worker concurrency |
| `coliseum_queue_length`, `coliseum_queue_pending`, `coliseum_dlq_length` | gauge | USE of the queue |

Health: `/healthz/live` (process), `/healthz/ready` (Redis PING < 500 ms; worker loop heartbeat < 30 s).
Logs: JSON with trace ids; every battle log line carries the battle id as a structured field.

## SLOs (30-day window)

| SLI | Objective | Alert (PrometheusRule in the chart) |
|-----|-----------|-------------------------------------|
| API availability (non-5xx) | 99.9 % | `ColiseumApiErrorBudgetBurn`: 5xx ratio > 1.44 % for 5 m (14.4× burn) → page |
| Submission → settlement p95 | ≤ 2 s for 99 % of 5-minute windows | `ColiseumQueueLatencyHigh`: p95 > 2 s for 10 m → page |
| Queue progress | pending never stalls | `ColiseumQueueStalled`: pending > 0 and no processing for 2 m → page |
| Poison messages | 0 | `ColiseumDeadLetterGrowing`: DLQ grew in 10 m → ticket |
| Duplicates | informational | `processed{result="duplicate"}` rising → ticket (crash loop or ack failures) |

Grafana dashboard `Coliseum` (Compose, `deploy/compose/grafana/coliseum.json`): API RED, queue USE, economy.

## Runbooks

| Runbook | Trigger |
|---------|---------|
| [RB-01 Backlog or old pending entries](runbooks/RB-01-backlog.md) | `coliseum_queue_pending` rising, latency alert |
| [RB-02 Worker crash loop](runbooks/RB-02-worker-crashloop.md) | pod restarts, `worker` readiness failing |
| [RB-03 Redis degraded](runbooks/RB-03-redis-degraded.md) | readiness failing on `redis`, PING slow |
| [RB-04 Duplicates rising](runbooks/RB-04-duplicates.md) | `result="duplicate"` counter |
| [RB-05 Leaderboard mismatch](runbooks/RB-05-leaderboard-mismatch.md) | support ticket |
| [RB-06 Rollout stuck](runbooks/RB-06-rollout-stuck.md) | Argo Rollouts paused / analysis inconclusive |

## Capacity (order of magnitude)

Engine ≈ microseconds per battle; settlement ≈ one Redis round trip (0.1–0.3 ms locally); one worker with
`MaxConcurrency = 2 × cores` is bounded by Redis, not CPU. A single small Redis handles thousands of settlements
per second, far beyond the exercise. Report size grows with turns (≤ 100 turns ≈ 20 KB JSON; the 10,000 stat cap
bounds the worst case).
