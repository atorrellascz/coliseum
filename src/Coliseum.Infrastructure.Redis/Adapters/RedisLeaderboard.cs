using Coliseum.Application.Ports;
using Coliseum.Contracts.Leaderboard;
using Coliseum.Domain.Players;
using Coliseum.Infrastructure.Redis.Keys;
using StackExchange.Redis;

namespace Coliseum.Infrastructure.Redis.Adapters;

/// <summary>
/// The leaderboard is a sorted set: ZINCRBY on settlement (inside the Lua script), ZREVRANGE WITHSCORES to read.
/// O(log N) per update, O(log N + M) per page. Equal scores are ordered by member, so ties are deterministic.
/// </summary>
public sealed class RedisLeaderboard(IConnectionMultiplexer redis, RedisKeys keys) : ILeaderboard
{
    public async Task<IReadOnlyList<LeaderboardEntry>> GetTopAsync(int offset, int limit, CancellationToken cancellationToken)
    {
        var entries = await redis.GetDatabase()
            .SortedSetRangeByRankWithScoresAsync(keys.Leaderboard, offset, offset + limit - 1, Order.Descending)
            .WaitAsync(cancellationToken);

        return entries.Select((e, i) => new LeaderboardEntry(offset + i + 1, (long)e.Score, (string)e.Element!)).ToList();
    }

    public Task<long> CountAsync(CancellationToken cancellationToken) =>
        redis.GetDatabase().SortedSetLengthAsync(keys.Leaderboard).WaitAsync(cancellationToken);

    public async Task<LeaderboardEntry?> GetEntryAsync(PlayerId playerId, CancellationToken cancellationToken)
    {
        var db = redis.GetDatabase();
        long? rank = await db.SortedSetRankAsync(keys.Leaderboard, playerId.Value, Order.Descending).WaitAsync(cancellationToken);
        if (rank is null)
        {
            return null;
        }

        double? score = await db.SortedSetScoreAsync(keys.Leaderboard, playerId.Value).WaitAsync(cancellationToken);
        return new LeaderboardEntry((int)rank.Value + 1, (long)(score ?? 0), playerId.Value);
    }
}
