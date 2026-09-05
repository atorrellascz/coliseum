using Coliseum.Application.Ports;
using Coliseum.Domain.Battles;

namespace Coliseum.UnitTests.Fakes;

/// <summary>Counters in memory with the same bucket rule as the Redis adapter.</summary>
internal sealed class InMemoryGameStats : IGameStats
{
    private readonly Dictionary<string, long> _buckets = GameStatsSnapshot.BucketNames.ToDictionary(b => b, _ => 0L, StringComparer.Ordinal);
    private long _processed;
    private long _attackerWins;
    private long _gold;
    private long _silver;
    private long _turns;

    public Task RecordBattleAsync(BattleReport report, SettlementResult settlement, CancellationToken cancellationToken)
    {
        _processed++;
        _attackerWins += report.AttackerWon ? 1 : 0;
        _gold += settlement.GoldTransferred;
        _silver += settlement.SilverTransferred;
        _turns += report.Turns;
        _buckets[GameStatsSnapshot.BucketFor(report.Turns)]++;
        return Task.CompletedTask;
    }

    public Task<GameStatsSnapshot> GetSnapshotAsync(CancellationToken cancellationToken) =>
        Task.FromResult(new GameStatsSnapshot(_processed, _attackerWins, _gold, _silver, _turns, new Dictionary<string, long>(_buckets, StringComparer.Ordinal)));
}
