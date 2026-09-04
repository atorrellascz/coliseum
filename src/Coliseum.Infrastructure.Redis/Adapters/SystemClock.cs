using Coliseum.Application.Ports;

namespace Coliseum.Infrastructure.Redis.Adapters;

/// <summary>Production clock. Wraps <see cref="TimeProvider"/> so hosts can still substitute a fake provider.</summary>
public sealed class SystemClock(TimeProvider timeProvider) : IClock
{
    public DateTimeOffset UtcNow => timeProvider.GetUtcNow();
}
