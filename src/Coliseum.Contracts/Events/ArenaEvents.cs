using System.Text.Json.Serialization;

namespace Coliseum.Contracts.Events;

/// <summary>
/// Live events published by the worker on Redis pub/sub and relayed by the API to SignalR clients (ADR-10).
/// Polymorphic JSON with a <c>type</c> discriminator so a JavaScript or Unity client can switch on it.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(BattleQueuedEvent), "battle.queued")]
[JsonDerivedType(typeof(BattleTurnEvent), "battle.turn")]
[JsonDerivedType(typeof(BattleDoneEvent), "battle.done")]
[JsonDerivedType(typeof(BattleFailedEvent), "battle.failed")]
[JsonDerivedType(typeof(LeaderboardChangedEvent), "leaderboard.changed")]
public abstract record ArenaEvent(DateTimeOffset OccurredAt)
{
    /// <summary>Player ids that should receive this event (their SignalR groups). Empty means broadcast only.</summary>
    public abstract IReadOnlyList<string> Recipients { get; }
}

public sealed record BattleQueuedEvent(DateTimeOffset OccurredAt, string BattleId, string AttackerId, string DefenderId)
    : ArenaEvent(OccurredAt)
{
    public override IReadOnlyList<string> Recipients => [AttackerId, DefenderId];
}

public sealed record BattleTurnEvent(
    DateTimeOffset OccurredAt,
    string BattleId,
    int Turn,
    string AttackerId,
    string DefenderId,
    bool Hit,
    int Damage,
    int DefenderHpAfter)
    : ArenaEvent(OccurredAt)
{
    public override IReadOnlyList<string> Recipients => [AttackerId, DefenderId];
}

public sealed record BattleDoneEvent(
    DateTimeOffset OccurredAt,
    string BattleId,
    string AttackerId,
    string DefenderId,
    string WinnerId,
    string LoserId,
    int Turns,
    int LootPercent,
    long GoldStolen,
    long SilverStolen,
    long Score)
    : ArenaEvent(OccurredAt)
{
    public override IReadOnlyList<string> Recipients => [AttackerId, DefenderId];
}

public sealed record BattleFailedEvent(DateTimeOffset OccurredAt, string BattleId, string AttackerId, string DefenderId, string Error)
    : ArenaEvent(OccurredAt)
{
    public override IReadOnlyList<string> Recipients => [AttackerId, DefenderId];
}

public sealed record LeaderboardChangedEvent(DateTimeOffset OccurredAt, IReadOnlyList<Leaderboard.LeaderboardEntry> Top)
    : ArenaEvent(OccurredAt)
{
    public override IReadOnlyList<string> Recipients => [];
}
