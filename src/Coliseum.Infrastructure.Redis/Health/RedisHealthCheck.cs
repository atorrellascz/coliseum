using System.Diagnostics;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace Coliseum.Infrastructure.Redis.Health;

/// <summary>Readiness: Redis answers PING within a budget. Slow answers are reported as degraded, not healthy.</summary>
public sealed class RedisHealthCheck(IConnectionMultiplexer redis) : IHealthCheck
{
    private static readonly TimeSpan DegradedThreshold = TimeSpan.FromMilliseconds(500);

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (!redis.IsConnected)
        {
            return HealthCheckResult.Unhealthy("Redis is not connected.");
        }

        try
        {
            long started = Stopwatch.GetTimestamp();
            await redis.GetDatabase().PingAsync().WaitAsync(cancellationToken);
            var elapsed = Stopwatch.GetElapsedTime(started);
            var data = new Dictionary<string, object> { ["pingMs"] = elapsed.TotalMilliseconds };

            return elapsed > DegradedThreshold
                ? HealthCheckResult.Degraded("Redis PING is slow.", data: data)
                : HealthCheckResult.Healthy("Redis PING ok.", data);
        }
        catch (Exception ex) when (ex is RedisException or TimeoutException)
        {
            return HealthCheckResult.Unhealthy("Redis PING failed.", ex);
        }
    }
}
