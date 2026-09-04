namespace Coliseum.Contracts.Battles;

/// <summary>
/// <c>GET /battles/{id}</c>. Before the battle is processed only the header fields are populated; once it is
/// <see cref="BattleStatus.Done"/> the outcome, the turn-by-turn events, a human-readable narrative and the loot
/// actually transferred are included. <paramref name="Error"/> is set only for <see cref="BattleStatus.Failed"/>.
/// </summary>
public sealed record BattleReportResponse(
    string BattleId,
    BattleStatus Status,
    string AttackerId,
    string DefenderId,
    DateTimeOffset SubmittedAt,
    DateTimeOffset? ProcessedAt,
    string? WinnerId,
    string? LoserId,
    int? Turns,
    ulong? Seed,
    LootResponse? Loot,
    IReadOnlyList<TurnResponse>? Events,
    IReadOnlyList<string>? Narrative,
    string? Error);

/// <summary>One turn, mirroring the domain event log.</summary>
public sealed record TurnResponse(
    int Turn,
    string AttackerId,
    string DefenderId,
    int AttackerHpBefore,
    int DefenderHpBefore,
    int AttackValueUsed,
    int DodgeChanceBasisPoints,
    int Roll,
    bool Hit,
    int Damage,
    int DefenderHpAfter);

/// <summary>What the winner took. <paramref name="Score"/> (gold + silver) is what was added to the leaderboard.</summary>
public sealed record LootResponse(int Percent, long Gold, long Silver, long Score);
