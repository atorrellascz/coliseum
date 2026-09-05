# RB-04: Duplicate deliveries rising

**Trigger:** `coliseum_battles_processed_total{result="duplicate"}` increasing.

## What it means
A message was processed after its battle had already been settled. Data is safe (the settlement is idempotent,
ADR-0003); the signal says acknowledgements are not landing: a worker crashed after settling and before `XACK`,
or `XACK` calls are timing out.

## Diagnose
1. Worker restarts (RB-02)? Each restart re-delivers everything that was in flight.
2. Redis timeouts in the worker logs around `XACK`? (RB-03)
3. `XPENDING` shows entries with `delivery count` > 1 that were settled: harmless, they will be acked on the next
   processing (duplicate outcome acks too).

## Mitigate
Fix the underlying restart or Redis issue. No data repair is needed.

## Verify
Duplicate rate returns to zero; `processed` continues to increase.
