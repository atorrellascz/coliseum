-- apply_battle.lua
-- Idempotent battle settlement (ADR-03). One atomic round trip does everything:
--   1. if the battle is already 'done', return the original amounts and touch nothing (re-delivery safe);
--   2. compute the loot on the loser's CURRENT balance with the same integer ceil the engine uses (SUP-04);
--   3. debit the loser, credit the winner (capped at ARGV[6], SUP-05), add the score to the leaderboard;
--   4. persist the report and mark the battle done.
--   KEYS[1] = battle:{id}      KEYS[2] = player:{winner}     KEYS[3] = player:{loser}     KEYS[4] = leaderboard
--   ARGV[1] = winner id        ARGV[2] = loser id            ARGV[3] = loot percent (5..10)
--   ARGV[4] = report JSON      ARGV[5] = processedAt (ISO)   ARGV[6] = max per resource (1e9)
-- Returns { applied, gold, silver }: applied = 1 (settled now), 0 (already settled), -1 (a player is missing).
local status = redis.call('HGET', KEYS[1], 'status')
if status == 'done' then
  return { 0, tonumber(redis.call('HGET', KEYS[1], 'gold')), tonumber(redis.call('HGET', KEYS[1], 'silver')) }
end

if redis.call('EXISTS', KEYS[2]) == 0 or redis.call('EXISTS', KEYS[3]) == 0 then
  return { -1, 0, 0 }
end

local percent = tonumber(ARGV[3])
local cap = tonumber(ARGV[6])

local loserGold = tonumber(redis.call('HGET', KEYS[3], 'gold'))
local loserSilver = tonumber(redis.call('HGET', KEYS[3], 'silver'))
local stolenGold = math.floor((loserGold * percent + 99) / 100)
local stolenSilver = math.floor((loserSilver * percent + 99) / 100)

local winnerGold = tonumber(redis.call('HGET', KEYS[2], 'gold'))
local winnerSilver = tonumber(redis.call('HGET', KEYS[2], 'silver'))

redis.call('HSET', KEYS[3], 'gold', loserGold - stolenGold, 'silver', loserSilver - stolenSilver)
redis.call('HSET', KEYS[2], 'gold', math.min(winnerGold + stolenGold, cap), 'silver', math.min(winnerSilver + stolenSilver, cap))
redis.call('ZINCRBY', KEYS[4], stolenGold + stolenSilver, ARGV[1])
redis.call('HSET', KEYS[1],
  'status', 'done',
  'winnerId', ARGV[1],
  'loserId', ARGV[2],
  'gold', stolenGold,
  'silver', stolenSilver,
  'score', stolenGold + stolenSilver,
  'report', ARGV[4],
  'processedAt', ARGV[5])

return { 1, stolenGold, stolenSilver }
