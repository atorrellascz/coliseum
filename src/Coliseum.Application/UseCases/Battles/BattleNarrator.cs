using System.Globalization;
using Coliseum.Application.Ports;
using Coliseum.Domain.Battles;
using Coliseum.Domain.Players;

namespace Coliseum.Application.UseCases.Battles;

/// <summary>
/// Turns the event log into sentences a player can read. Pure presentation: it never re-runs the engine, it only
/// reads the facts in the report. Names fall back to ids when a player is unknown.
/// </summary>
public static class BattleNarrator
{
    public static IReadOnlyList<string> Narrate(BattleReport report, SettlementResult settlement, IReadOnlyDictionary<PlayerId, string> names)
    {
        var lines = new List<string>(report.Events.Count + 2);
        var culture = CultureInfo.InvariantCulture;

        lines.Add(string.Create(culture, $"{Name(report.AttackerId)} challenges {Name(report.DefenderId)}. Seed {report.Seed}."));

        foreach (var e in report.Events)
        {
            lines.Add(e.Hit
                ? string.Create(culture, $"Turn {e.Turn}: {Name(e.AttackerId)} hits {Name(e.DefenderId)} for {e.Damage} (attack {e.AttackValueUsed}). {Name(e.DefenderId)} has {e.DefenderHpAfter} HP left.")
                : string.Create(culture, $"Turn {e.Turn}: {Name(e.AttackerId)} misses {Name(e.DefenderId)} (roll {e.Roll} below dodge {e.DodgeChanceBasisPoints}). {Name(e.DefenderId)} keeps {e.DefenderHpAfter} HP."));
        }

        lines.Add(string.Create(
            culture,
            $"{Name(report.WinnerId)} wins after {report.Turns} turns and steals {settlement.GoldTransferred} gold and {settlement.SilverTransferred} silver ({report.Loot.Percent}%) from {Name(report.LoserId)}."));

        return lines;

        string Name(PlayerId id) => names.TryGetValue(id, out string? name) ? name : id.Value;
    }
}
