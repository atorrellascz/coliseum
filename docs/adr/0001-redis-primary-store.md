# ADR-0001: Redis as the primary store

- Status: Accepted
- Date: 2026-09-03

## Context and decision
The spec recommends Redis. Every access in this system is by key (player by id, battle by id), the leaderboard is
the textbook use of a sorted set, and the queue needs durable delivery with acknowledgements. Redis 7 provides all
three (hashes, sorted sets, streams) plus pub/sub for live events and Lua for atomic multi-key operations, so a
second system is not needed for the exercise.

## Consequences
- Data model in `docs/redis-data-model.md`: hashes for players and battles, one sorted set for the leaderboard,
  two streams (queue and dead-letter), one pub/sub channel. Every key is built in `RedisKeys`.
- Redis must run with `maxmemory-policy noeviction` and AOF (`appendonly yes`): evicting a `battle:{id}` would let a
  re-delivered message be settled twice. Compose, the Helm chart and the Terraform parameter group all set this.
- No secondary indexes or ad-hoc queries: `GET /players` reads a creation-time sorted set (`players:index`) and
  nothing else scans. Analytics would go to a separate store.
- Single instance in the exercise. Redis Cluster would require hash tags so that the keys touched by one Lua
  script (`battle:{id}`, both players, the leaderboard) live in one slot, or splitting the settlement.
