using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Coliseum.Worker.Processing;

/// <summary>Last time the processing loop completed an iteration. Written by the loop, read by the health check.</summary>
public sealed class WorkerHeartbeat(TimeProvider timeProvider)
{
    private long _lastBeatTicks;

    public DateTimeOffset? LastBeat
    {
        get
        {
            long ticks = Interlocked.Read(ref _lastBeatTicks);
            return ticks == 0 ? null : new DateTimeOffset(ticks, TimeSpan.Zero);
        }
    }

    public void Beat() => Interlocked.Exchange(ref _lastBeatTicks, timeProvider.GetUtcNow().UtcTicks);
}

/// <summary>Readiness of the worker itself: the loop must have run recently. Redis readiness is a separate check.</summary>
public sealed class WorkerHealthCheck(WorkerHeartbeat heartbeat, TimeProvider timeProvider) : IHealthCheck
{
    private static readonly TimeSpan StaleAfter = TimeSpan.FromSeconds(30);

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var last = heartbeat.LastBeat;
        if (last is null)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("Processing loop has not started."));
        }

        var age = timeProvider.GetUtcNow() - last.Value;
        return Task.FromResult(age > StaleAfter
            ? HealthCheckResult.Unhealthy($"Processing loop stalled for {age.TotalSeconds:F0}s.")
            : HealthCheckResult.Healthy("Processing loop alive."));
    }
}
