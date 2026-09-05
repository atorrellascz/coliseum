using Coliseum.Application.Ports;
using Coliseum.Contracts.Admin;

namespace Coliseum.Application.UseCases.Admin;

/// <summary>Back-office snapshot: economy counters, queue USE numbers and the top of the leaderboard, in one call.</summary>
public sealed class GetAdminStatsHandler(IGameStats stats, IBattleQueue queue, ILeaderboard leaderboard, IClock clock)
{
    public const int TopSize = 10;

    public async Task<AdminStatsResponse> HandleAsync(CancellationToken cancellationToken)
    {
        var snapshot = await stats.GetSnapshotAsync(cancellationToken);
        var queueStats = await queue.GetStatsAsync(cancellationToken);
        var top = await leaderboard.GetTopAsync(0, TopSize, cancellationToken);

        return new AdminStatsResponse(
            clock.UtcNow,
            new EconomyStats(
                snapshot.BattlesProcessed,
                snapshot.AttackerWins,
                Math.Round(snapshot.AttackerWinRate, 4),
                snapshot.GoldStolen,
                snapshot.SilverStolen,
                Math.Round(snapshot.AverageTurns, 2),
                snapshot.TurnBuckets),
            new QueueStatsResponse(queueStats.Length, queueStats.Pending, queueStats.DeadLettered),
            top);
    }
}
