using Coliseum.Application.Ports;
using Coliseum.Contracts.Leaderboard;
using Coliseum.Domain.Players;

namespace Coliseum.UnitTests.Fakes;

/// <summary>Sorted-set stand-in. Ties are ordered like ZREVRANGE: equal scores come out in descending member order.</summary>
internal sealed class InMemoryLeaderboard : ILeaderboard
{
    private readonly Dictionary<PlayerId, long> _scores = [];

    public void Add(PlayerId playerId, long score) => _scores[playerId] = _scores.GetValueOrDefault(playerId) + score;

    public long ScoreOf(PlayerId playerId) => _scores.GetValueOrDefault(playerId);

    public Task<IReadOnlyList<LeaderboardEntry>> GetTopAsync(int offset, int limit, CancellationToken cancellationToken)
    {
        IReadOnlyList<LeaderboardEntry> page = Ranked().Skip(offset).Take(limit).ToList();
        return Task.FromResult(page);
    }

    public Task<long> CountAsync(CancellationToken cancellationToken) => Task.FromResult((long)_scores.Count);

    public Task<LeaderboardEntry?> GetEntryAsync(PlayerId playerId, CancellationToken cancellationToken) =>
        Task.FromResult(Ranked().FirstOrDefault(e => e.PlayerId == playerId.Value));

    private IEnumerable<LeaderboardEntry> Ranked() =>
        _scores
            .OrderByDescending(pair => pair.Value)
            .ThenByDescending(pair => pair.Key.Value, StringComparer.Ordinal)
            .Select((pair, index) => new LeaderboardEntry(index + 1, pair.Value, pair.Key.Value));
}
