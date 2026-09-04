using Coliseum.Domain.Common;

namespace Coliseum.Domain.Players;

/// <summary>
/// Immutable value object for the fighting attributes. The spec gives no ranges, so these are explicit assumptions (SUP-11):
/// attack and hit points are at least 1 (a battle must be able to end), defense may be 0 (never dodges),
/// and everything is capped at <see cref="MaxStat"/> so a single battle has a bounded number of turns and a bounded report size.
/// </summary>
public readonly record struct CombatStats
{
    public const int MaxStat = 10_000;

    private CombatStats(int attack, int defense, int hitPoints)
    {
        Attack = attack;
        Defense = defense;
        HitPoints = hitPoints;
    }

    public int Attack { get; }

    public int Defense { get; }

    public int HitPoints { get; }

    /// <summary>Validates external input. Reports every attribute that is out of range.</summary>
    public static Result<CombatStats> Create(int attack, int defense, int hitPoints)
    {
        var errors = new List<DomainError>(3);

        if (attack is < 1 or > MaxStat)
        {
            errors.Add(DomainError.Validation("attack", "player.attack.out_of_range", "Attack must be between 1 and 10,000."));
        }

        if (defense is < 0 or > MaxStat)
        {
            errors.Add(DomainError.Validation("defense", "player.defense.out_of_range", "Defense must be between 0 and 10,000."));
        }

        if (hitPoints is < 1 or > MaxStat)
        {
            errors.Add(DomainError.Validation("hitPoints", "player.hit_points.out_of_range", "Hit points must be between 1 and 10,000."));
        }

        return errors.Count == 0 ? Result.Ok(new CombatStats(attack, defense, hitPoints)) : Result.Fail<CombatStats>(errors);
    }

    /// <summary>Wraps values already known to be valid (storage reads). Never use it for user input.</summary>
    public static CombatStats Unchecked(int attack, int defense, int hitPoints) => new(attack, defense, hitPoints);
}
