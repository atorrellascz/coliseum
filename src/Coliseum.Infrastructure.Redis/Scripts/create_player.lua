-- create_player.lua
-- Atomic player creation with name uniqueness.
--   KEYS[1] = player:name:{NORMALIZED}   (uniqueness guard)
--   KEYS[2] = player:{id}                (hash)
--   KEYS[3] = players:index              (sorted set by creation time)
--   ARGV[1] = player id
--   ARGV[2] = createdAt as unix milliseconds (index score)
--   ARGV[3..] = hash field/value pairs
-- Returns 1 when created, 0 when the name is already taken (nothing written).
-- Two concurrent requests with the same name: exactly one SET NX succeeds; Redis runs the script atomically.
if redis.call('SET', KEYS[1], ARGV[1], 'NX') == false then
  return 0
end
redis.call('HSET', KEYS[2], unpack(ARGV, 3))
redis.call('ZADD', KEYS[3], ARGV[2], ARGV[1])
return 1
