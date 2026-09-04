using Coliseum.Domain.Common;

namespace Coliseum.Domain.Players;

/// <summary>
/// Immutable value object for a player's wealth. Invariant: each resource is within [0, 1e9] (spec: "Max: 1 billion").
/// The constructor is private, so any <see cref="Resources"/> in the system already satisfies the invariant;
/// arithmetic is saturating rather than throwing (SUP-05: excess above the cap is burned, a balance never goes negative).
/// </summary>
public readonly record struct Resources
{
    public const long MaxPerResource = 1_000_000_000;

    private Resources(long gold, long silver)
    {
        Gold = gold;
        Silver = silver;
    }

    public long Gold { get; }

    public long Silver { get; }

    public static Resources Zero => default;

    /// <summary>Validates external input. Reports both resources when both are wrong.</summary>
    public static Result<Resources> Create(long gold, long silver)
    {
        var errors = new List<DomainError>(2);

        if (gold is < 0 or > MaxPerResource)
        {
            errors.Add(DomainError.Validation("gold", "player.gold.out_of_range", "Gold must be between 0 and 1,000,000,000."));
        }

        if (silver is < 0 or > MaxPerResource)
        {
            errors.Add(DomainError.Validation("silver", "player.silver.out_of_range", "Silver must be between 0 and 1,000,000,000."));
        }

        return errors.Count == 0 ? Result.Ok(new Resources(gold, silver)) : Result.Fail<Resources>(errors);
    }

    /// <summary>Wraps values already known to be valid (storage reads). Never use it for user input.</summary>
    public static Resources Unchecked(long gold, long silver) => new(gold, silver);

    /// <summary>Saturating addition: the result never exceeds <see cref="MaxPerResource"/>.</summary>
    public Resources Plus(Resources other) =>
        new(Math.Min(Gold + other.Gold, MaxPerResource), Math.Min(Silver + other.Silver, MaxPerResource));

    /// <summary>Saturating subtraction: the result never goes below zero.</summary>
    public Resources Minus(Resources other) =>
        new(Math.Max(Gold - other.Gold, 0), Math.Max(Silver - other.Silver, 0));

    /// <summary>
    /// The share stolen at <paramref name="percent"/>, each resource individually and rounded up.
    /// Spec example: 500 gold and 120 silver at 7% give 35 gold and 9 silver (8.4 rounds up to 9).
    /// </summary>
    public Resources Percent(int percent) =>
        new(IntegerMath.CeilPercent(Gold, percent), IntegerMath.CeilPercent(Silver, percent));
}
