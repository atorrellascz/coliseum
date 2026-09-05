# RB-05: "The leaderboard is wrong"

**Trigger:** support ticket; a player's score does not match their battle history.

## Diagnose
The leaderboard is derived data: score = sum of `score` over the battles a player won. Recompute and compare:

```
# every settled battle stores winnerId and score in its hash
redis-cli --scan --pattern 'coliseum:battle:*' | while read k; do
  redis-cli HMGET "$k" status winnerId score
done | awk '$1=="done"{s[$2]+=$3} END{for (p in s) print p, s[p]}' | sort -k2 -nr > recomputed.txt
redis-cli ZREVRANGE coliseum:leaderboard 0 -1 WITHSCORES | paste - - > current.txt
diff <(sort recomputed.txt) <(sort current.txt)
```

A mismatch can only come from manual edits of Redis or a bug in `apply_battle.lua` (settlement and `ZINCRBY` happen
in the same script, so partial application is not possible).

## Mitigate
Rebuild the sorted set from the recomputed file (`ZADD` per player) during a short write pause of the worker
(scale to 0, rebuild, scale back), then investigate how the divergence happened.

## Verify
`diff` is empty; `GET /leaderboard` matches the sum of `loot.score` over `GET /battles/{id}` for a sample of players.
