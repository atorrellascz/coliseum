namespace Coliseum.Contracts.Battles;

/// <summary>
/// Body of <c>POST /battles</c>. <paramref name="AttackerId"/> is optional: a player token always attacks as
/// itself (the value must match if present), a service token must specify it.
/// </summary>
public sealed record SubmitBattleRequest(string? DefenderId, string? AttackerId = null);
