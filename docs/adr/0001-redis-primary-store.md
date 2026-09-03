# ADR-0001: Redis as the primary store

- Status: Proposed
- Date: 2026-09-03

## Context and decision
The spec recommends Redis; every access is by key, the leaderboard is a sorted set and the queue is a durable stream with acks. Requires maxmemory-policy=noeviction and AOF.

## Consequences
_To be completed when the micro-project that implements it lands._
