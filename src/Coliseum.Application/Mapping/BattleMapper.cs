using Coliseum.Application.Ports;
using Coliseum.Application.UseCases.Battles;
using Coliseum.Contracts.Battles;
using Coliseum.Domain.Players;

namespace Coliseum.Application.Mapping;

/// <summary>Builds the API view of a battle record, including the narrative when the report exists.</summary>
public static class BattleMapper
{
    public static BattleReportResponse ToResponse(BattleRecord record, IReadOnlyDictionary<PlayerId, string> names)
    {
        var report = record.Report;
        var settlement = record.Settlement;

        return new BattleReportResponse(
            record.BattleId.Value,
            record.Status,
            record.AttackerId.Value,
            record.DefenderId.Value,
            record.SubmittedAt,
            record.ProcessedAt,
            report?.WinnerId.Value,
            report?.LoserId.Value,
            report?.Turns,
            report?.Seed,
            report is null || settlement is null
                ? null
                : new LootResponse(report.Loot.Percent, settlement.GoldTransferred, settlement.SilverTransferred, settlement.Score),
            report?.Events.Select(e => new TurnResponse(
                e.Turn,
                e.AttackerId.Value,
                e.DefenderId.Value,
                e.AttackerHpBefore,
                e.DefenderHpBefore,
                e.AttackValueUsed,
                e.DodgeChanceBasisPoints,
                e.Roll,
                e.Hit,
                e.Damage,
                e.DefenderHpAfter)).ToList(),
            report is null || settlement is null ? null : BattleNarrator.Narrate(report, settlement, names),
            record.Error);
    }
}
