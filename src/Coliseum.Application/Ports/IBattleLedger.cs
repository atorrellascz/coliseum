using Coliseum.Domain.Battles;

namespace Coliseum.Application.Ports;

/// <summary>
/// Settles a finished battle: debit the loser, credit the winner, add the score to the leaderboard and persist
/// the report, all as one atomic and idempotent operation keyed by the battle id (ADR-03). Applying the same
/// report twice returns <see cref="SettlementOutcome.AlreadyApplied"/> and changes nothing.
/// </summary>
public interface IBattleLedger
{
    Task<SettlementResult> ApplyAsync(BattleReport report, CancellationToken cancellationToken);
}

public enum SettlementOutcome
{
    /// <summary>Balances and leaderboard were updated by this call.</summary>
    Applied,

    /// <summary>The battle had already been settled; the returned amounts are the original ones.</summary>
    AlreadyApplied,

    /// <summary>One of the players no longer exists; nothing was changed (SUP-08).</summary>
    PlayerMissing,
}

/// <summary>
/// What was actually transferred. Computed on the loser's live balance at settlement time (SUP-04), so it can be
/// lower than the engine's estimate if the loser lost resources in between.
/// </summary>
public sealed record SettlementResult(SettlementOutcome Outcome, long GoldTransferred, long SilverTransferred)
{
    /// <summary>Score credited to the winner on the leaderboard.</summary>
    public long Score => GoldTransferred + SilverTransferred;
}
