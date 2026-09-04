using Coliseum.Contracts.Events;

namespace Coliseum.Application.Ports;

/// <summary>
/// Fire-and-forget fan-out of live events (PAT-11). The worker publishes; the API relays to SignalR groups.
/// Losing an event must never fail a battle: adapters swallow and log transport errors.
/// </summary>
public interface IEventPublisher
{
    Task PublishAsync(ArenaEvent arenaEvent, CancellationToken cancellationToken);
}
