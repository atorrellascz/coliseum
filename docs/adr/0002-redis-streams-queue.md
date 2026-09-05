# ADR-0002: Redis Streams with a consumer group as the battle queue

- Status: Accepted
- Date: 2026-09-03

## Context and decision
"Handle battles in the order they are submitted; none processed more than once, none skipped." A Redis list with
`BLMOVE` gives order but no acknowledgement; SQS or RabbitMQ would add a system. A Redis Stream with a consumer
group gives monotonic ids (submission order), a Pending Entries List until `XACK`, `XAUTOCLAIM` to recover
entries from dead consumers, and `MAXLEN ~` trimming.

## Consequences
- `RedisBattleQueue` implements `IBattleQueue`: `XADD` on submit, `XREADGROUP` (non-blocking, ADR-0016) in the
  worker, `XACK` only after the settlement, `XAUTOCLAIM` every 15 s for entries idle ≥ 30 s, dead-letter stream
  after 5 deliveries. Delivery is at-least-once; ADR-0003 makes the effect exactly-once.
- Queue depth, pending count and dead-letter length are exposed as gauges and drive the SLO alerts.
- Ordering across several worker replicas is per replica (ADR-0006).
- Integration tests cover order of 100 entries, crash-and-reclaim with a rising delivery count, and dead-lettering.
