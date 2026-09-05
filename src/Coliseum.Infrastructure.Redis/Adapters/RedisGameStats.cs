using Coliseum.Application.Ports;
using Coliseum.Domain.Battles;
using Coliseum.Infrastructure.Redis.Keys;
using StackExchange.Redis;

namespace Coliseum.Infrastructure.Redis.Adapters;

/// <summary>
/// Game counters in one hash (<c>stats:battles</c>), updated with pipelined HINCRBY after every settlement.
/// Counters are eventually consistent with the settlement (they are not inside the Lua script on purpose: a lost
/// increment costs a slightly off dashboard, never a wrong balance), and idempotency is inherited from the caller,
/// which only records on <see cref="SettlementOutcome.Applied"/>.
/// </summary>
public sealed class RedisGameStats(IConnectionMultiplexer redis, RedisKeys keys) : IGameStats
{
    private const string Processed = "processed";
    private const string AttackerWins = "attackerWins";
    private const string Gold = "gold";
    private const string Silver = "silver";
    private const string Turns = "turns";
    private const string BucketPrefix = "turns:";

    public async Task RecordBattleAsync(BattleReport report, SettlementResult settlement, CancellationToken cancellationToken)
    {
        var db = redis.GetDatabase();
        var key = keys.Stats;
        var batch = db.CreateBatch();
        var writes = new List<Task>
        {
            batch.HashIncrementAsync(key, Processed),
            batch.HashIncrementAsync(key, Gold, settlement.GoldTransferred),
            batch.HashIncrementAsync(key, Silver, settlement.SilverTransferred),
            batch.HashIncrementAsync(key, Turns, report.Turns),
            batch.HashIncrementAsync(key, BucketPrefix + GameStatsSnapshot.BucketFor(report.Turns)),
        };
        if (report.AttackerWon)
        {
            writes.Add(batch.HashIncrementAsync(key, AttackerWins));
        }

        batch.Execute();
        await Task.WhenAll(writes).WaitAsync(cancellationToken);
    }

    public async Task<GameStatsSnapshot> GetSnapshotAsync(CancellationToken cancellationToken)
    {
        var entries = await redis.GetDatabase().HashGetAllAsync(keys.Stats).WaitAsync(cancellationToken);
        var map = entries.ToDictionary(e => (string)e.Name!, e => (long)e.Value, StringComparer.Ordinal);

        var buckets = GameStatsSnapshot.BucketNames.ToDictionary(name => name, name => map.GetValueOrDefault(BucketPrefix + name), StringComparer.Ordinal);

        return new GameStatsSnapshot(
            map.GetValueOrDefault(Processed),
            map.GetValueOrDefault(AttackerWins),
            map.GetValueOrDefault(Gold),
            map.GetValueOrDefault(Silver),
            map.GetValueOrDefault(Turns),
            buckets);
    }
}
