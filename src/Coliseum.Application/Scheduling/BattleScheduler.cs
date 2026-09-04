using Coliseum.Application.Ports;
using Coliseum.Domain.Players;

namespace Coliseum.Application.Scheduling;

/// <summary>
/// Decides which queued battles may run right now (ADR-05, PAT-09). Guarantees:
/// <list type="number">
/// <item>Two running battles never share a player.</item>
/// <item>For any player, battles start in submission order: a blocked battle <b>reserves</b> its players so no later
/// battle involving them can overtake it.</item>
/// <item>At most <see cref="MaxConcurrency"/> battles run at once.</item>
/// </list>
/// Single-writer by design: one loop owns this object (enqueue, dispatch, complete). It holds no locks and does
/// no I/O, which is what makes the guarantees easy to test exhaustively.
/// </summary>
public sealed class BattleScheduler
{
    private readonly LinkedList<ScheduledBattle> _pending = new();
    private readonly Dictionary<string, ScheduledBattle> _running = new(StringComparer.Ordinal);
    private readonly HashSet<PlayerId> _busy = [];

    public BattleScheduler(int maxConcurrency)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxConcurrency, 1);
        MaxConcurrency = maxConcurrency;
    }

    public int MaxConcurrency { get; }

    public int PendingCount => _pending.Count;

    public int RunningCount => _running.Count;

    /// <summary>Adds a message at the back of the pending list. Order of calls is the order of the stream.</summary>
    public void Enqueue(QueuedBattle message, DateTimeOffset now) => _pending.AddLast(new ScheduledBattle(message, now));

    /// <summary>
    /// Returns every pending battle that can start now, in order, and marks them running. Walks the pending list
    /// front to back; a battle that cannot start reserves its two players so that later battles with either of
    /// them are skipped this round (no overtaking).
    /// </summary>
    public IReadOnlyList<ScheduledBattle> Dispatch()
    {
        var started = new List<ScheduledBattle>();
        var reserved = new HashSet<PlayerId>();

        var node = _pending.First;
        while (node is not null && _running.Count < MaxConcurrency)
        {
            var next = node.Next;
            var message = node.Value.Message;
            bool attackerFree = !_busy.Contains(message.AttackerId) && !reserved.Contains(message.AttackerId);
            bool defenderFree = !_busy.Contains(message.DefenderId) && !reserved.Contains(message.DefenderId);

            if (attackerFree && defenderFree)
            {
                _pending.Remove(node);
                _running[message.MessageId] = node.Value;
                _busy.Add(message.AttackerId);
                _busy.Add(message.DefenderId);
                started.Add(node.Value);
            }
            else
            {
                reserved.Add(message.AttackerId);
                reserved.Add(message.DefenderId);
            }

            node = next;
        }

        return started;
    }

    /// <summary>Frees the players of a finished battle. Returns false when the id is unknown (already completed).</summary>
    public bool Complete(string messageId)
    {
        if (!_running.Remove(messageId, out var battle))
        {
            return false;
        }

        _busy.Remove(battle.Message.AttackerId);
        _busy.Remove(battle.Message.DefenderId);
        return true;
    }

    public bool IsBusy(PlayerId playerId) => _busy.Contains(playerId);
}
