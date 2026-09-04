using Coliseum.Application.Ports;
using Coliseum.Contracts.Events;

namespace Coliseum.UnitTests.Fakes;

/// <summary>Captures published events so tests can assert on them.</summary>
internal sealed class RecordingEventPublisher(List<string>? log = null) : IEventPublisher
{
    private readonly List<ArenaEvent> _events = [];

    public IReadOnlyList<ArenaEvent> Events => _events;

    public Task PublishAsync(ArenaEvent arenaEvent, CancellationToken cancellationToken)
    {
        log?.Add("events:publish");
        _events.Add(arenaEvent);
        return Task.CompletedTask;
    }

    public T Single<T>() where T : ArenaEvent => _events.OfType<T>().Single();
}
