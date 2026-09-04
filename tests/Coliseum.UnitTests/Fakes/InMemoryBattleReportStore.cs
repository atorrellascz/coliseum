using Coliseum.Application.Ports;
using Coliseum.Contracts.Battles;
using Coliseum.Domain.Battles;
using Coliseum.Domain.Players;

namespace Coliseum.UnitTests.Fakes;

/// <summary>Dictionary of battle records with the same lifecycle transitions as the Redis hash.</summary>
internal sealed class InMemoryBattleReportStore(List<string>? log = null) : IBattleReportStore
{
    private readonly Dictionary<BattleId, BattleRecord> _records = [];

    public IReadOnlyDictionary<BattleId, BattleRecord> All => _records;

    public Task CreateQueuedAsync(BattleId battleId, PlayerId attackerId, PlayerId defenderId, DateTimeOffset submittedAt, CancellationToken cancellationToken)
    {
        log?.Add("reports:create");
        _records[battleId] = new BattleRecord(battleId, BattleStatus.Queued, attackerId, defenderId, submittedAt, null, null, null, null);
        return Task.CompletedTask;
    }

    public Task<BattleRecord?> GetAsync(BattleId battleId, CancellationToken cancellationToken) =>
        Task.FromResult(_records.GetValueOrDefault(battleId));

    public Task MarkProcessingAsync(BattleId battleId, CancellationToken cancellationToken)
    {
        Update(battleId, r => r with { Status = BattleStatus.Processing });
        return Task.CompletedTask;
    }

    public Task MarkFailedAsync(BattleId battleId, string reason, DateTimeOffset failedAt, CancellationToken cancellationToken)
    {
        Update(battleId, r => r with { Status = BattleStatus.Failed, Error = reason, ProcessedAt = failedAt });
        return Task.CompletedTask;
    }

    /// <summary>The Done transition belongs to the ledger; the in-memory ledger calls this.</summary>
    public void MarkDone(BattleId battleId, BattleReport report, SettlementResult settlement, DateTimeOffset processedAt) =>
        Update(battleId, r => r with { Status = BattleStatus.Done, Report = report, Settlement = settlement, ProcessedAt = processedAt, Error = null });

    private void Update(BattleId battleId, Func<BattleRecord, BattleRecord> change)
    {
        if (_records.TryGetValue(battleId, out var record))
        {
            _records[battleId] = change(record);
        }
    }
}
