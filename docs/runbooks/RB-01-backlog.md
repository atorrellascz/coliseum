# RB-01: Backlog or old pending entries

**Trigger:** `coliseum_queue_pending` grows, `ColiseumQueueLatencyHigh`, or players report battles stuck in `queued`.

## Diagnose
1. Is the worker alive? `kubectl -n coliseum get pods -l app.kubernetes.io/component=worker`; readiness must be
   `1/1`. Logs: `kubectl logs deploy/coliseum-coliseum-worker --tail 200`.
2. Queue state in Redis:
   ```
   XINFO GROUPS coliseum:battles:stream        # pending count, last-delivered-id
   XPENDING coliseum:battles:stream workers - + 10   # oldest pending entries, idle time, delivery count
   XLEN coliseum:battles:dlq
   ```
3. Is it throughput or a stall? `rate(coliseum_battles_processed_total[1m])` > 0 means slow; 0 means stuck (see RB-02).
4. Is Redis slow? `coliseum_battle_processing_duration_seconds` p95 rising together with Redis latency points to RB-03.

## Mitigate
- Slow but progressing: raise `Worker__MaxConcurrency` (Helm `worker.maxConcurrency`) if CPU allows; the work is
  I/O bound.
- Entries pending for a dead consumer are reclaimed automatically after 30 s (`XAUTOCLAIM`); if a consumer name
  keeps them for longer, the worker owning it is hung: restart that pod.
- Many workers are not the answer without partitioning (ADR-0006).

## Verify
Pending returns to ~0, `coliseum_battle_queue_latency_seconds` p95 < 2 s, no growth in the DLQ.
