using Coliseum.Contracts.Leaderboard;
using Coliseum.Domain.Players;

namespace Coliseum.Application.Ports;

/// <summary>
/// Ranked view of players by total resources stolen. Writes happen inside the settlement (<see cref="IBattleLedger"/>);
/// this port is read-only. Ties are broken deterministically by the adapter (Redis orders equal scores lexicographically by member).
/// </summary>
public interface ILeaderboard
{
    /// <summary>Entries from <paramref name="offset"/> (0-based), at most <paramref name="limit"/>, best first. Rank is absolute, not page-relative.</summary>
    Task<IReadOnlyList<LeaderboardEntry>> GetTopAsync(int offset, int limit, CancellationToken cancellationToken);

    Task<long> CountAsync(CancellationToken cancellationToken);

    /// <summary>The entry of one player, or null when the player has no score yet.</summary>
    Task<LeaderboardEntry?> GetEntryAsync(PlayerId playerId, CancellationToken cancellationToken);
}
