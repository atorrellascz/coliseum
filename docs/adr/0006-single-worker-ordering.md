# ADR-0006: One worker replica gives strict global order; partition the stream to scale

- Status: Accepted
- Date: 2026-09-03

## Context and decision
With one consumer, stream order is processing order and the scheduler (ADR-0005) keeps per-player order while
running disjoint battles in parallel. With N consumers on one stream, each consumer keeps order among the entries
it received, but two consumers may process entries from different players in any relative order. The exercise
ships one replica (the Helm chart pins `worker.replicas: 1` with a `Recreate` strategy) and documents the scaling
path instead of pretending the multi-replica case is solved.

## Consequences
- Throughput of one worker: the engine costs microseconds, the settlement one Redis round trip; with
  `MaxConcurrency` = 2 × cores, thousands of battles per second are possible before ordering becomes the limit.
- Scaling path: partition into `battles:stream:{hash(attackerId) % N}` with one worker per partition. Per-player
  order is preserved because a player's submissions always land in the same partition. The scheduler code does not
  change; `RedisBattleQueue` gets a partition key.
- Correctness never depends on the replica count: the settlement is atomic and idempotent regardless (ADR-0003).
