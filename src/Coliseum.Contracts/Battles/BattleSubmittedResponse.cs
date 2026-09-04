namespace Coliseum.Contracts.Battles;

/// <summary><c>202 Accepted</c> payload: the id to poll (or to subscribe to over SignalR) and the initial status.</summary>
public sealed record BattleSubmittedResponse(string BattleId, BattleStatus Status, DateTimeOffset SubmittedAt);
