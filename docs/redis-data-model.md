# Redis data model

All keys carry the configurable prefix (`coliseum` by default), so several environments can share one server.

| Key | Type | Fields / members | Written by |
|-----|------|------------------|-----------|
| `{p}:player:{id}` | hash | `id, name, description, gold, silver, attack, defense, hitPoints, createdAt` (ISO 8601) | `create_player.lua`; balances by `apply_battle.lua` |
| `{p}:player:name:{NORMALIZED}` | string | player id | `create_player.lua` (`SET NX`, uniqueness guard) |
| `{p}:players:index` | sorted set | score = created-at unix ms, member = id | `create_player.lua` |
| `{p}:battles:stream` | stream | `battleId, attackerId, defenderId, submittedAt`; `MAXLEN ~ 1,000,000` | API (`XADD`) |
| `{p}:battles:dlq` | stream | same fields + `deliveryCount, reason` | worker after 5 failed deliveries |
| `{p}:battle:{id}` | hash | `status (queued\|processing\|done\|failed), attackerId, defenderId, submittedAt, winnerId, loserId, gold, silver, score, report (JSON), processedAt, error` | API (`queued`), `mark_battle.lua`, `apply_battle.lua` |
| `{p}:leaderboard` | sorted set | score = total stolen, member = player id | `apply_battle.lua` (`ZINCRBY`) |
| `{p}:arena:events` | pub/sub channel | JSON `ArenaEvent` with `type` discriminator | API (`battle.queued`), worker (`battle.done`, `battle.failed`) |

Consumer group on the stream: `workers` (configurable). Each worker process uses a unique consumer name.

## Scripts

| Script | Keys | Purpose |
|--------|------|---------|
| `create_player.lua` | name guard, player hash, index | Reserve the normalized name; only then write the player. Returns 1 or 0. |
| `apply_battle.lua` | battle, winner, loser, leaderboard | Idempotent settlement. Returns `{applied, gold, silver}` with `applied` = 1 / 0 (already done) / -1 (player missing). |
| `mark_battle.lua` | battle | Set `processing` or `failed` unless the battle is already `done`. |

Scripts are embedded in `Coliseum.Infrastructure.Redis`; StackExchange.Redis sends `EVALSHA` and falls back to
`EVAL` on `NOSCRIPT`, so restarts of Redis need no special handling.

## Operational settings

- `maxmemory-policy noeviction`: evicting a `battle:{id}` would break idempotency (a re-delivered message would be
  settled twice). Prefer failing writes over silent data loss.
- `appendonly yes` (`everysec`): the stream and the balances survive a restart.

## Useful commands

```
XINFO GROUPS coliseum:battles:stream          # pending per group, last delivered id
XPENDING coliseum:battles:stream workers      # in-flight entries, idle times
XLEN coliseum:battles:dlq                     # poison messages
ZREVRANGE coliseum:leaderboard 0 9 WITHSCORES # top 10
HGETALL coliseum:battle:<id>                  # one battle, including the report JSON
```
