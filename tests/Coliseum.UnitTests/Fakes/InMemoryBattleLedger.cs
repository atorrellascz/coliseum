using Coliseum.Application.Ports;
using Coliseum.Contracts.Battles;
using Coliseum.Domain.Battles;

namespace Coliseum.UnitTests.Fakes;

/// <summary>
/// Mirrors what apply_battle.lua does, in memory: idempotent on battle id, loot recomputed on the loser's live
/// balance, winner capped, loser floored, leaderboard incremented, record marked Done. All or nothing.
/// </summary>
internal sealed class InMemoryBattleLedger(
    InMemoryPlayerRepository players,
    InMemoryLeaderboard leaderboard,
    InMemoryBattleReportStore reports,
    IClock clock) : IBattleLedger
{
    public int Applications { get; private set; }

    public Task<SettlementResult> ApplyAsync(BattleReport report, CancellationToken cancellationToken)
    {
        var record = reports.All.GetValueOrDefault(report.BattleId);
        if (record is { Status: BattleStatus.Done, Settlement: not null })
        {
            return Task.FromResult(record.Settlement with { Outcome = SettlementOutcome.AlreadyApplied });
        }

        var winner = players.All.GetValueOrDefault(report.WinnerId);
        var loser = players.All.GetValueOrDefault(report.LoserId);
        if (winner is null || loser is null)
        {
            return Task.FromResult(new SettlementResult(SettlementOutcome.PlayerMissing, 0, 0));
        }

        var stolen = loser.Resources.Percent(report.Loot.Percent);
        players.Replace(loser.WithResources(loser.Resources.Minus(stolen)));
        players.Replace(winner.WithResources(winner.Resources.Plus(stolen)));

        var settlement = new SettlementResult(SettlementOutcome.Applied, stolen.Gold, stolen.Silver);
        leaderboard.Add(report.WinnerId, settlement.Score);
        reports.MarkDone(report.BattleId, report, settlement, clock.UtcNow);
        Applications++;

        return Task.FromResult(settlement);
    }
}
