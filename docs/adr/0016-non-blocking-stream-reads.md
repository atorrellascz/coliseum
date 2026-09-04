# ADR-0016: Non-blocking stream reads with a short poll instead of XREADGROUP BLOCK

- Status: Accepted
- Date: 2026-09-05

## Context
The natural way to consume a Redis Stream is `XREADGROUP ... BLOCK <ms>`. StackExchange.Redis multiplexes every
command over one connection and therefore does not support blocking commands: a blocked read would stall every
other caller of the multiplexer.

## Decision
`RedisBattleQueue.ReadAsync` issues a non-blocking `XREADGROUP` and, when nothing is returned, the worker loop
waits for either a completion of an in-flight battle or `Worker:PollInterval` (250 ms by default) before
reading again. While battles are flowing the loop never sleeps.

## Consequences
- Idle latency between submission and pickup is bounded by the poll interval (p50 ≈ 125 ms, p100 ≈ 250 ms);
  the spec asks for "immediate processing, no strict real-time requirement".
- One idle `XREADGROUP` every 250 ms per worker: negligible load.
- Alternative kept for later: a dedicated second connection issuing `XREADGROUP BLOCK` through `ExecuteAsync`.
  Not worth the complexity for this exercise.
