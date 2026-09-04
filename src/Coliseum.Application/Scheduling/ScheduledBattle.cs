using Coliseum.Application.Ports;

namespace Coliseum.Application.Scheduling;

/// <summary>A queued message as tracked by the scheduler, with the time it entered the in-memory pending list.</summary>
public sealed record ScheduledBattle(QueuedBattle Message, DateTimeOffset EnqueuedAt)
{
    public string MessageId => Message.MessageId;
}
