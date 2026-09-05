using Coliseum.Contracts.Leaderboard;

namespace Coliseum.Contracts.Admin;

/// <summary><c>GET /admin/stats</c> (service token): what an operator or a product owner wants on one screen.</summary>
public sealed record AdminStatsResponse(
    DateTimeOffset GeneratedAt,
    EconomyStats Economy,
    QueueStatsResponse Queue,
    IReadOnlyList<LeaderboardEntry> Top);

/// <summary>Exact totals since the store was created, plus the balance signal (attacker win rate).</summary>
public sealed record EconomyStats(
    long BattlesProcessed,
    long AttackerWins,
    double AttackerWinRate,
    long GoldStolen,
    long SilverStolen,
    double AverageTurns,
    IReadOnlyDictionary<string, long> TurnBuckets);

/// <summary>USE of the queue: length (saturation), pending (in flight), dead-lettered (errors).</summary>
public sealed record QueueStatsResponse(long Length, long Pending, long DeadLettered);
