namespace Coliseum.Domain.Common;

/// <summary>
/// Integer-only percentage helpers (ADR-07). No floating point anywhere in the engine, so the same inputs
/// give bit-identical results on every platform and in every language that re-implements the formulas
/// (the Lua settlement script uses exactly these expressions).
/// </summary>
public static class IntegerMath
{
    /// <summary>ceil(value * percent / 100) computed as (value * percent + 99) / 100. Spec: "always rounded up".</summary>
    public static long CeilPercent(long value, int percent)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        ArgumentOutOfRangeException.ThrowIfNegative(percent);
        return (value * percent + 99) / 100;
    }

    /// <summary>floor(value * percent / 100). Used for the proportional attack decay.</summary>
    public static long FloorPercent(long value, int percent)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        ArgumentOutOfRangeException.ThrowIfNegative(percent);
        return value * percent / 100;
    }
}
