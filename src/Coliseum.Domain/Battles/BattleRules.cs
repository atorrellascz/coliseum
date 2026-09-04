using Coliseum.Domain.Common;

namespace Coliseum.Domain.Battles;

/// <summary>
/// Tunable parameters of the battle system. Defaults are the spec values; anything the spec leaves open is an
/// explicit assumption (SUP-01 dodge cap, SUP-12 turn guard). Hosts bind this from configuration (Options pattern)
/// and validate it at start-up, so a bad value fails the deployment rather than a battle.
/// </summary>
/// <param name="MinAttackPercent">Floor of the attack decay as a percentage of the base attack. Spec: 50.</param>
/// <param name="MinLootPercent">Lowest share of each resource the winner steals. Spec: 5.</param>
/// <param name="MaxLootPercent">Highest share of each resource the winner steals. Spec: 10.</param>
/// <param name="MaxDodgeBasisPoints">Cap on the dodge chance, in basis points (7500 = 75%). Guarantees every attack has at least a 25% chance to land, so every battle terminates.</param>
/// <param name="MaxTurns">Safety guard: a battle exceeding it fails with an invariant error instead of looping forever. With <c>CombatStats.MaxStat</c> = 10,000 the worst realistic case is ~40,000 turns.</param>
public sealed record BattleRules(
    int MinAttackPercent = 50,
    int MinLootPercent = 5,
    int MaxLootPercent = 10,
    int MaxDodgeBasisPoints = 7_500,
    int MaxTurns = 100_000)
{
    public static BattleRules Default { get; } = new();

    /// <summary>Returns every configuration problem; empty when the rules are usable.</summary>
    public IReadOnlyList<DomainError> Validate()
    {
        var errors = new List<DomainError>();

        if (MinAttackPercent is < 1 or > 100)
        {
            errors.Add(DomainError.Validation(nameof(MinAttackPercent), "rules.min_attack_percent", "MinAttackPercent must be between 1 and 100."));
        }

        if (MinLootPercent < 0 || MaxLootPercent > 100 || MinLootPercent > MaxLootPercent)
        {
            errors.Add(DomainError.Validation(nameof(MinLootPercent), "rules.loot_percent", "Loot percent range must satisfy 0 <= min <= max <= 100."));
        }

        if (MaxDodgeBasisPoints is < 0 or >= BattleEngine.RollRange)
        {
            errors.Add(DomainError.Validation(nameof(MaxDodgeBasisPoints), "rules.max_dodge", "MaxDodgeBasisPoints must be between 0 and 9,999 so attacks can always land."));
        }

        if (MaxTurns < 1)
        {
            errors.Add(DomainError.Validation(nameof(MaxTurns), "rules.max_turns", "MaxTurns must be at least 1."));
        }

        return errors;
    }
}
