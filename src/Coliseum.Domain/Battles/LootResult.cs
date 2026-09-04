using Coliseum.Domain.Players;

namespace Coliseum.Domain.Battles;

/// <summary>
/// What the winner takes from the loser. One percentage per battle, drawn in [MinLootPercent, MaxLootPercent],
/// applied to each resource individually and rounded up (SUP-03). The amounts here are computed on the loser's
/// balance as the engine saw it; the settlement script recomputes them on the live balance with the same formula
/// (SUP-04), so the report always shows what was actually transferred.
/// </summary>
/// <param name="Percent">Share stolen from each resource.</param>
/// <param name="Gold">Gold stolen.</param>
/// <param name="Silver">Silver stolen.</param>
public sealed record LootResult(int Percent, long Gold, long Silver)
{
    /// <summary>Total value moved: this is the score submitted to the leaderboard.</summary>
    public long Total => Gold + Silver;

    public static LootResult Compute(Resources loserResources, int percent)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(percent);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(percent, 100);

        Resources stolen = loserResources.Percent(percent);
        return new LootResult(percent, stolen.Gold, stolen.Silver);
    }
}
