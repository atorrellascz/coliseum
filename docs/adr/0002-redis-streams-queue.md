# ADR-0002: Redis Streams + consumer group as the battle queue

- Status: Proposed
- Date: 2026-09-03

## Context and decision
XADD ids are monotonic (FIFO), PEL + XACK give at-least-once, XAUTOCLAIM recovers orphaned messages, a dead-letter stream holds poison messages.

## Consequences
_To be completed when the micro-project that implements it lands._
