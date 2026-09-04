namespace Coliseum.Application.Ports;

/// <summary>Injected time source. Use cases never call <c>DateTimeOffset.UtcNow</c>, so tests can freeze time.</summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
