using Coliseum.Application.Ports;

namespace Coliseum.UnitTests.Fakes;

/// <summary>Clock that only moves when a test says so.</summary>
internal sealed class FixedClock(DateTimeOffset start) : IClock
{
    public DateTimeOffset UtcNow { get; private set; } = start;

    public void Advance(TimeSpan by) => UtcNow += by;
}

/// <summary>Predictable ids: id-0001, id-0002, ...</summary>
internal sealed class SequentialIdGenerator(string prefix = "id") : IIdGenerator
{
    private int _next;

    public string NewId() => $"{prefix}-{++_next:D4}";
}

/// <summary>Issues inspectable fake tokens: "token:{role}:{playerId}".</summary>
internal sealed class FakeTokenService(IClock clock) : IAuthTokenService
{
    public IssuedToken Issue(Coliseum.Application.Caller caller) =>
        new($"token:{caller.Role}:{caller.PlayerId?.Value}", clock.UtcNow.AddHours(1));
}
