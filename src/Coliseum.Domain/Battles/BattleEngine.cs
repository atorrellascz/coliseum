using Coliseum.Domain.Common;
using Coliseum.Domain.Players;
using Coliseum.Domain.Randomness;

namespace Coliseum.Domain.Battles;

/// <summary>
/// The battle simulator. A pure function of (battle id, attacker, defender, rules): no I/O, no clock, no shared state,
/// integer arithmetic only. Run it twice with the same inputs and you get the same report, which is what makes
/// crash-safe reprocessing, replays and golden tests possible.
/// </summary>
public static class BattleEngine
{
    /// <summary>Resolution of the hit roll: chances are expressed in basis points, 0-10,000.</summary>
    public const int RollRange = 10_000;

    /// <summary>
    /// Simulates a battle. Returns a validation error when a player fights themselves and an invariant error when
    /// the turn guard is exceeded (a rules bug, never a legitimate outcome).
    /// </summary>
    /// <param name="battleId">Identifies the battle and seeds its randomness.</param>
    /// <param name="attacker">The initiator; attacks first (spec rule 1).</param>
    /// <param name="defender">The opponent.</param>
    /// <param name="rules">Tunables; <see cref="BattleRules.Default"/> when null.</param>
    /// <param name="random">Randomness source; a <see cref="Xoshiro256StarStar"/> seeded from the id when null. Tests inject scripted rolls.</param>
    public static Result<BattleReport> Run(
        BattleId battleId,
        Player attacker,
        Player defender,
        BattleRules? rules = null,
        IBattleRandom? random = null)
    {
        rules ??= BattleRules.Default;

        if (attacker.Id == defender.Id)
        {
            return Result.Fail<BattleReport>(DomainError.Validation("defenderId", "battle.self", "A player cannot battle themselves."));
        }

        ulong seed = SeedDerivation.FromString(battleId.Value);
        random ??= new Xoshiro256StarStar(seed);

        var first = new Combatant(attacker);
        var second = new Combatant(defender);
        var events = new List<TurnEvent>();

        // The initiator strikes first; roles swap after every turn (spec rule 1).
        Combatant current = first;
        Combatant other = second;

        while (first.IsAlive && second.IsAlive)
        {
            if (events.Count >= rules.MaxTurns)
            {
                return Result.Fail<BattleReport>(DomainError.Invariant("battle.max_turns", "Battle exceeded the maximum number of turns."));
            }

            int attackValue = CurrentAttack(current.BaseAttack, current.HitPoints, current.MaxHitPoints, rules.MinAttackPercent);
            int dodgeChance = DodgeBasisPoints(other.Defense, attackValue, rules.MaxDodgeBasisPoints);
            int roll = random.Roll(0, RollRange);
            bool hit = roll >= dodgeChance;
            int damage = hit ? attackValue : 0;
            int defenderHpBefore = other.HitPoints;

            other.HitPoints = Math.Max(0, defenderHpBefore - damage);

            events.Add(new TurnEvent(
                Turn: events.Count + 1,
                AttackerId: current.Id,
                DefenderId: other.Id,
                AttackerHpBefore: current.HitPoints,
                DefenderHpBefore: defenderHpBefore,
                AttackValueUsed: attackValue,
                DodgeChanceBasisPoints: dodgeChance,
                Roll: roll,
                Hit: hit,
                Damage: damage,
                DefenderHpAfter: other.HitPoints));

            (current, other) = (other, current);
        }

        Combatant winner = first.IsAlive ? first : second;
        Combatant loser = first.IsAlive ? second : first;

        // The loot percentage is drawn after the fight so it never disturbs the turn sequence.
        int lootPercent = random.Roll(rules.MinLootPercent, rules.MaxLootPercent + 1);
        LootResult loot = LootResult.Compute(loser.Resources, lootPercent);

        return Result.Ok(new BattleReport(
            battleId,
            seed,
            attacker.Id,
            defender.Id,
            winner.Id,
            loser.Id,
            events.Count,
            events,
            loot,
            first.HitPoints,
            second.HitPoints));
    }

    /// <summary>
    /// Attack after decay (spec rule 3): proportional to the remaining health, never below
    /// <paramref name="minAttackPercent"/> of the base. Spec example: base 70, 90/100 hp gives 63; the floor is 35.
    /// </summary>
    public static int CurrentAttack(int baseAttack, int hitPoints, int maxHitPoints, int minAttackPercent)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxHitPoints);
        ArgumentOutOfRangeException.ThrowIfNegative(hitPoints);

        long floor = IntegerMath.CeilPercent(baseAttack, minAttackPercent);
        long scaled = (long)baseAttack * hitPoints / maxHitPoints;
        return (int)Math.Max(floor, scaled);
    }

    /// <summary>
    /// Dodge chance in basis points (spec rule 2 leaves the formula open, SUP-01):
    /// defense / (defense + attack), capped at <paramref name="maxDodgeBasisPoints"/>.
    /// Monotonic in defense, bounded, independent of the stat scale, and 0 when there is no defense.
    /// </summary>
    public static int DodgeBasisPoints(int defense, int attackValue, int maxDodgeBasisPoints)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(defense);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(attackValue);

        if (defense == 0)
        {
            return 0;
        }

        long basisPoints = (long)defense * RollRange / ((long)defense + attackValue);
        return (int)Math.Min(basisPoints, maxDodgeBasisPoints);
    }
}
