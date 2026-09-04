using Coliseum.Contracts.Battles;
using Coliseum.Domain.Battles;
using Coliseum.Domain.Players;

namespace Coliseum.Application.Ports;

/// <summary>
/// Read side and lifecycle marks of a battle record (<c>battle:{id}</c>). The <see cref="BattleStatus.Done"/>
/// transition is not here on purpose: it happens inside <see cref="IBattleLedger"/> so status, balances and
/// leaderboard change together.
/// </summary>
public interface IBattleReportStore
{
    /// <summary>Writes the record in <see cref="BattleStatus.Queued"/> state, before the message is enqueued (PAT-10: never a message without a record).</summary>
    Task CreateQueuedAsync(BattleId battleId, PlayerId attackerId, PlayerId defenderId, DateTimeOffset submittedAt, CancellationToken cancellationToken);

    Task<BattleRecord?> GetAsync(BattleId battleId, CancellationToken cancellationToken);

    Task MarkProcessingAsync(BattleId battleId, CancellationToken cancellationToken);

    Task MarkFailedAsync(BattleId battleId, string reason, DateTimeOffset failedAt, CancellationToken cancellationToken);
}

/// <summary>Everything stored about a battle. <paramref name="Report"/> and <paramref name="Settlement"/> exist only when <see cref="Status"/> is Done.</summary>
public sealed record BattleRecord(
    BattleId BattleId,
    BattleStatus Status,
    PlayerId AttackerId,
    PlayerId DefenderId,
    DateTimeOffset SubmittedAt,
    DateTimeOffset? ProcessedAt,
    BattleReport? Report,
    SettlementResult? Settlement,
    string? Error)
{
    public bool Involves(PlayerId playerId) => AttackerId == playerId || DefenderId == playerId;
}
