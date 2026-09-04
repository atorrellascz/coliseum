using System.Globalization;
using System.Text.Json;
using Coliseum.Application.Ports;
using Coliseum.Contracts.Battles;
using Coliseum.Domain.Battles;
using Coliseum.Domain.Players;
using Coliseum.Infrastructure.Redis.Keys;
using Coliseum.Infrastructure.Redis.Scripts;
using Coliseum.Infrastructure.Redis.Serialization;
using StackExchange.Redis;

namespace Coliseum.Infrastructure.Redis.Adapters;

/// <summary>
/// The <c>battle:{id}</c> hash: created in <c>queued</c> state before the message exists (PAT-10), moved to
/// <c>processing</c>/<c>failed</c> through <c>mark_battle.lua</c> (never overwriting <c>done</c>), and read back
/// including the JSON report and the settled amounts written by the ledger script.
/// </summary>
public sealed class RedisBattleReportStore(IConnectionMultiplexer redis, RedisKeys keys, LuaScripts scripts) : IBattleReportStore
{
    public Task CreateQueuedAsync(BattleId battleId, PlayerId attackerId, PlayerId defenderId, DateTimeOffset submittedAt, CancellationToken cancellationToken)
    {
        HashEntry[] entries =
        [
            new("status", "queued"),
            new("attackerId", attackerId.Value),
            new("defenderId", defenderId.Value),
            new("submittedAt", submittedAt.ToString("O", CultureInfo.InvariantCulture)),
        ];
        return redis.GetDatabase().HashSetAsync(keys.Battle(battleId), entries).WaitAsync(cancellationToken);
    }

    public async Task<BattleRecord?> GetAsync(BattleId battleId, CancellationToken cancellationToken)
    {
        var entries = await redis.GetDatabase().HashGetAllAsync(keys.Battle(battleId)).WaitAsync(cancellationToken);
        if (entries.Length == 0)
        {
            return null;
        }

        var map = entries.ToDictionary(e => (string)e.Name!, e => e.Value, StringComparer.Ordinal);
        var status = ParseStatus((string)map["status"]!);

        BattleReport? report = null;
        SettlementResult? settlement = null;
        if (status == BattleStatus.Done)
        {
            report = JsonSerializer.Deserialize((string)map["report"]!, ColiseumJsonContext.Default.BattleReport);
            settlement = new SettlementResult(SettlementOutcome.Applied, (long)map["gold"], (long)map["silver"]);
        }

        return new BattleRecord(
            battleId,
            status,
            PlayerId.Unchecked((string)map["attackerId"]!),
            PlayerId.Unchecked((string)map["defenderId"]!),
            ParseDate(map["submittedAt"])!.Value,
            ParseDate(map.GetValueOrDefault("processedAt")),
            report,
            settlement,
            (string?)map.GetValueOrDefault("error"));
    }

    public Task MarkProcessingAsync(BattleId battleId, CancellationToken cancellationToken) =>
        Mark(battleId, "processing", string.Empty, string.Empty, cancellationToken);

    public Task MarkFailedAsync(BattleId battleId, string reason, DateTimeOffset failedAt, CancellationToken cancellationToken) =>
        Mark(battleId, "failed", reason, failedAt.ToString("O", CultureInfo.InvariantCulture), cancellationToken);

    private async Task Mark(BattleId battleId, string status, string error, string processedAt, CancellationToken cancellationToken) =>
        await redis.GetDatabase()
            .ScriptEvaluateAsync(scripts.MarkBattle, [keys.Battle(battleId)], [status, error, processedAt])
            .WaitAsync(cancellationToken);

    private static BattleStatus ParseStatus(string value) => value switch
    {
        "queued" => BattleStatus.Queued,
        "processing" => BattleStatus.Processing,
        "done" => BattleStatus.Done,
        "failed" => BattleStatus.Failed,
        _ => throw new InvalidOperationException("Unknown battle status in storage: " + value),
    };

    private static DateTimeOffset? ParseDate(RedisValue value) =>
        value.IsNullOrEmpty ? null : DateTimeOffset.Parse((string)value!, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
}
