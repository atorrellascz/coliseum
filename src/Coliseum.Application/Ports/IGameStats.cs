using Coliseum.Domain.Battles;

namespace Coliseum.Application.Ports;

/// <summary>
/// Aggregated game numbers for the back-office (OPS-03): the economy of the game as counters, cheap to update
/// (a few HINCRBY per settlement) and cheap to read. Not a replacement for metrics: these survive restarts and
/// are exact totals, while Prometheus gives rates and percentiles.
/// </summary>
public interface IGameStats
{
    Task RecordBattleAsync(BattleReport report, SettlementResult settlement, CancellationToken cancellationToken);

    Task<GameStatsSnapshot> GetSnapshotAsync(CancellationToken cancellationToken);
}

/// <summary>Totals since the store was created. <paramref name="TurnBuckets"/> keys are "1-5", "6-10", "11-20", "21-50", "51+".</summary>
public sealed record GameStatsSnapshot(
    long BattlesProcessed,
    long AttackerWins,
    long GoldStolen,
    long SilverStolen,
    long TotalTurns,
    IReadOnlyDictionary<string, long> TurnBuckets)
{
    public static readonly IReadOnlyList<string> BucketNames = ["1-5", "6-10", "11-20", "21-50", "51+"];

    public static string BucketFor(int turns) => turns switch
    {
        <= 5 => "1-5",
        <= 10 => "6-10",
        <= 20 => "11-20",
        <= 50 => "21-50",
        _ => "51+",
    };

    public double AttackerWinRate => BattlesProcessed == 0 ? 0 : (double)AttackerWins / BattlesProcessed;

    public double AverageTurns => BattlesProcessed == 0 ? 0 : (double)TotalTurns / BattlesProcessed;
}
