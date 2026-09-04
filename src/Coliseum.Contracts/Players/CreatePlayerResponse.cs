namespace Coliseum.Contracts.Players;

/// <summary>
/// <c>201 Created</c> payload: the player plus a player-scoped bearer token, so a freshly created player can
/// immediately submit battles without a separate login step.
/// </summary>
public sealed record CreatePlayerResponse(PlayerResponse Player, string AccessToken, DateTimeOffset ExpiresAt);
