-- mark_battle.lua
-- Status transition guarded against overwriting a settled battle: a late or duplicate worker must never turn a
-- 'done' battle back into 'processing' or 'failed'.
--   KEYS[1] = battle:{id}
--   ARGV[1] = new status ('processing' | 'failed')
--   ARGV[2] = error text ('' when none)
--   ARGV[3] = processedAt (ISO, '' when none)
-- Returns 1 when written, 0 when skipped.
if redis.call('HGET', KEYS[1], 'status') == 'done' then
  return 0
end
redis.call('HSET', KEYS[1], 'status', ARGV[1])
if ARGV[2] ~= '' then
  redis.call('HSET', KEYS[1], 'error', ARGV[2], 'processedAt', ARGV[3])
end
return 1
