using System.Globalization;
using System.Text.Json;
using Coliseum.Application.Ports;
using Coliseum.Domain.Battles;
using Coliseum.Domain.Players;
using Coliseum.Infrastructure.Redis.Keys;
using Coliseum.Infrastructure.Redis.Scripts;
using Coliseum.Infrastructure.Redis.Serialization;
using StackExchange.Redis;

namespace Coliseum.Infrastructure.Redis.Adapters;

/// <summary>
/// Settlement through <c>apply_battle.lua</c>: one atomic, idempotent round trip (ADR-03). The engine's loot
/// percentage is passed in; the amounts are recomputed by the script on the live balance (SUP-04).
/// </summary>
public sealed class RedisBattleLedger(IConnectionMultiplexer redis, RedisKeys keys, LuaScripts scripts, IClock clock) : IBattleLedger
{
    public async Task<SettlementResult> ApplyAsync(BattleReport report, CancellationToken cancellationToken)
    {
        RedisKey[] scriptKeys = [keys.Battle(report.BattleId), keys.Player(report.WinnerId), keys.Player(report.LoserId), keys.Leaderboard];
        RedisValue[] args =
        [
            report.WinnerId.Value,
            report.LoserId.Value,
            report.Loot.Percent,
            JsonSerializer.Serialize(report, ColiseumJsonContext.Default.BattleReport),
            clock.UtcNow.ToString("O", CultureInfo.InvariantCulture),
            Resources.MaxPerResource,
        ];

        var result = (RedisResult[])(await redis.GetDatabase().ScriptEvaluateAsync(scripts.ApplyBattle, scriptKeys, args).WaitAsync(cancellationToken))!;
        long applied = (long)result[0];
        long gold = (long)result[1];
        long silver = (long)result[2];

        return applied switch
        {
            1 => new SettlementResult(SettlementOutcome.Applied, gold, silver),
            0 => new SettlementResult(SettlementOutcome.AlreadyApplied, gold, silver),
            _ => new SettlementResult(SettlementOutcome.PlayerMissing, 0, 0),
        };
    }
}
