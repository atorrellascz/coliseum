namespace Coliseum.Contracts.Leaderboard;

/// <summary>Exactly what the spec asks for: rank, score and player identifier. Rank is 1-based.</summary>
public sealed record LeaderboardEntry(int Rank, long Score, string PlayerId);
