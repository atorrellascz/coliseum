using Coliseum.Domain.Battles;
using Coliseum.Domain.Players;

namespace Coliseum.Application.Ports;

/// <summary>
/// The battle request queue (ADR-02). Semantics are those of a Redis Stream with a consumer group:
/// messages are delivered at least once, stay pending until acknowledged, and can be reclaimed from a dead consumer.
/// The in-memory fake in the unit tests honours the same contract, verified by a shared contract test.
/// </summary>
public interface IBattleQueue
{
    /// <summary>Creates the stream and consumer group if they do not exist. Safe to call on every start.</summary>
    Task InitializeAsync(CancellationToken cancellationToken);

    /// <summary>Appends a request. Ordering is the order of successful calls.</summary>
    Task EnqueueAsync(BattleId battleId, PlayerId attackerId, PlayerId defenderId, DateTimeOffset submittedAt, CancellationToken cancellationToken);

    /// <summary>Delivers up to <paramref name="count"/> new messages to <paramref name="consumer"/>, waiting at most <paramref name="block"/> when empty.</summary>
    Task<IReadOnlyList<QueuedBattle>> ReadAsync(string consumer, int count, TimeSpan block, CancellationToken cancellationToken);

    /// <summary>Takes over messages that another consumer received but did not acknowledge within <paramref name="minIdle"/>.</summary>
    Task<IReadOnlyList<QueuedBattle>> ClaimStaleAsync(string consumer, TimeSpan minIdle, int count, CancellationToken cancellationToken);

    /// <summary>Marks a message as fully processed. Only called after the settlement succeeded.</summary>
    Task AcknowledgeAsync(string messageId, CancellationToken cancellationToken);

    /// <summary>Moves a poison message to the dead-letter stream and acknowledges it, so it never blocks the queue nor disappears silently.</summary>
    Task DeadLetterAsync(QueuedBattle battle, string reason, CancellationToken cancellationToken);

    Task<QueueStats> GetStatsAsync(CancellationToken cancellationToken);
}

/// <summary>A delivered message. <paramref name="MessageId"/> is the stream entry id used to acknowledge it.</summary>
/// <param name="DeliveryCount">How many times the message has been delivered; the worker dead-letters after a threshold.</param>
public sealed record QueuedBattle(
    string MessageId,
    BattleId BattleId,
    PlayerId AttackerId,
    PlayerId DefenderId,
    DateTimeOffset SubmittedAt,
    int DeliveryCount);

/// <summary>USE metrics of the queue: length = saturation, pending = in flight, dead-lettered = errors.</summary>
public sealed record QueueStats(long Length, long Pending, long DeadLettered);
