namespace Coliseum.Contracts.Leaderboard;

/// <summary><c>GET /leaderboard?offset=&amp;limit=</c>. <paramref name="Total"/> is the number of ranked players.</summary>
public sealed record LeaderboardResponse(IReadOnlyList<LeaderboardEntry> Entries, int Offset, int Limit, long Total);
