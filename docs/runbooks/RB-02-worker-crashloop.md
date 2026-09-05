# RB-02: Worker crash loop or stuck loop

**Trigger:** worker pod restarts, `worker` readiness unhealthy ("Processing loop stalled"), `ColiseumQueueStalled`.

## Diagnose
1. `kubectl logs deploy/coliseum-coliseum-worker --previous` for the crash reason. Start-up failures are usually
   configuration (options validate on start: Redis connection string, rules) and appear in the first lines.
2. A loop that is alive but stalled logs "Redis unavailable; backing off" repeatedly → RB-03.
3. A single poison entry: look for "threw on delivery" with the same battle id; after 5 deliveries it goes to
   the DLQ and the queue continues. If the same id keeps throwing before reaching 5, the worker is being
   restarted before the count grows: check liveness settings.

## Mitigate
- Configuration: fix the value (Helm values / Secret) and roll the deployment.
- Poison entry: let it dead-letter, or move it manually: `XADD coliseum:battles:dlq * battleId <id> reason manual`
  then `XACK coliseum:battles:stream workers <entry-id>`.
- Bug in the engine or settlement: the battle is marked `failed` with the error code; fix, deploy, and replay
  from the DLQ (`scripts/replay-dlq.sh`, or `XRANGE` the DLQ and re-`XADD` to the stream — settlement is
  idempotent, so replaying an already settled battle is harmless).

## Verify
Pod `1/1`, `coliseum_battles_processed_total{result="processed"}` increasing, DLQ length stable.
