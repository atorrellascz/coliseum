namespace Coliseum.Contracts.Players;

/// <summary>Public view of a player. Ids travel as plain strings on the wire.</summary>
public sealed record PlayerResponse(
    string Id,
    string Name,
    string Description,
    long Gold,
    long Silver,
    int Attack,
    int Defense,
    int HitPoints,
    DateTimeOffset CreatedAt);
