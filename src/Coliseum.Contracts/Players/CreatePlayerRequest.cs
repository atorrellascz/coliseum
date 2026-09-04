namespace Coliseum.Contracts.Players;

/// <summary>
/// Body of <c>POST /players</c>. Everything is nullable or primitive on purpose: the transport layer never
/// rejects a request for shape reasons, the domain validates and reports every rule that was broken.
/// </summary>
public sealed record CreatePlayerRequest(
    string? Name,
    string? Description,
    long Gold,
    long Silver,
    int Attack,
    int Defense,
    int HitPoints);
