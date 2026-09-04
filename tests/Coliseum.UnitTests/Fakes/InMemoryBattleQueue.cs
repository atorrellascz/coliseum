using Coliseum.Application.Ports;
using Coliseum.Domain.Battles;
using Coliseum.Domain.Players;

namespace Coliseum.UnitTests.Fakes;

/// <summary>
/// In-memory stream with consumer-group semantics: delivered messages stay pending until acknowledged and can be
/// claimed back after they sit idle. Delivery count grows on every delivery, like XPENDING reports it.
/// </summary>
internal sealed class InMemoryBattleQueue(IClock clock, List<string>? log = null) : IBattleQueue
{
    private readonly List<Entry> _entries = [];
    private readonly List<(QueuedBattle Battle, string Reason)> _deadLetters = [];
    private int _nextId;

    public IReadOnlyList<(QueuedBattle Battle, string Reason)> DeadLetters => _deadLetters;

    public bool Initialized { get; private set; }

    public Task InitializeAsync(CancellationToken cancellationToken)
    {
        Initialized = true;
        return Task.CompletedTask;
    }

    public Task EnqueueAsync(BattleId battleId, PlayerId attackerId, PlayerId defenderId, DateTimeOffset submittedAt, CancellationToken cancellationToken)
    {
        log?.Add("queue:enqueue");
        _entries.Add(new Entry($"{++_nextId}-0", battleId, attackerId, defenderId, submittedAt));
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<QueuedBattle>> ReadAsync(string consumer, int count, TimeSpan block, CancellationToken cancellationToken)
    {
        var delivered = _entries.Where(e => e.State == State.New).Take(count).Select(e => Deliver(e, consumer)).ToList();
        return Task.FromResult<IReadOnlyList<QueuedBattle>>(delivered);
    }

    public Task<IReadOnlyList<QueuedBattle>> ClaimStaleAsync(string consumer, TimeSpan minIdle, int count, CancellationToken cancellationToken)
    {
        var claimed = _entries
            .Where(e => e.State == State.Pending && clock.UtcNow - e.DeliveredAt >= minIdle)
            .Take(count)
            .Select(e => Deliver(e, consumer))
            .ToList();
        return Task.FromResult<IReadOnlyList<QueuedBattle>>(claimed);
    }

    public Task AcknowledgeAsync(string messageId, CancellationToken cancellationToken)
    {
        var entry = _entries.Single(e => e.MessageId == messageId);
        entry.State = State.Acked;
        return Task.CompletedTask;
    }

    public Task DeadLetterAsync(QueuedBattle battle, string reason, CancellationToken cancellationToken)
    {
        _deadLetters.Add((battle, reason));
        return AcknowledgeAsync(battle.MessageId, cancellationToken);
    }

    public Task<QueueStats> GetStatsAsync(CancellationToken cancellationToken) =>
        Task.FromResult(new QueueStats(
            _entries.Count,
            _entries.Count(e => e.State == State.Pending),
            _deadLetters.Count));

    private QueuedBattle Deliver(Entry entry, string consumer)
    {
        entry.State = State.Pending;
        entry.Consumer = consumer;
        entry.DeliveredAt = clock.UtcNow;
        entry.DeliveryCount++;
        return new QueuedBattle(entry.MessageId, entry.BattleId, entry.AttackerId, entry.DefenderId, entry.SubmittedAt, entry.DeliveryCount);
    }

    private enum State
    {
        New,
        Pending,
        Acked,
    }

    private sealed class Entry(string messageId, BattleId battleId, PlayerId attackerId, PlayerId defenderId, DateTimeOffset submittedAt)
    {
        public string MessageId { get; } = messageId;

        public BattleId BattleId { get; } = battleId;

        public PlayerId AttackerId { get; } = attackerId;

        public PlayerId DefenderId { get; } = defenderId;

        public DateTimeOffset SubmittedAt { get; } = submittedAt;

        public State State { get; set; } = State.New;

        public string? Consumer { get; set; }

        public DateTimeOffset DeliveredAt { get; set; }

        public int DeliveryCount { get; set; }
    }
}
