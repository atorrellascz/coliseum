using System.Text.Json;
using Coliseum.Application.Ports;
using Coliseum.Contracts.Events;
using Coliseum.Infrastructure.Redis.Keys;
using Coliseum.Infrastructure.Redis.Serialization;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Coliseum.Infrastructure.Redis.Adapters;

/// <summary>
/// Publishes live events on a pub/sub channel (PAT-11). Fire-and-forget by contract: a transport problem is
/// logged and swallowed, because losing a notification must never fail a battle that is already settled.
/// </summary>
public sealed partial class RedisEventPublisher(IConnectionMultiplexer redis, RedisKeys keys, ILogger<RedisEventPublisher> logger) : IEventPublisher
{
    public async Task PublishAsync(ArenaEvent arenaEvent, CancellationToken cancellationToken)
    {
        string payload = JsonSerializer.Serialize(arenaEvent, ColiseumJsonContext.Default.ArenaEvent);
        try
        {
            await redis.GetSubscriber().PublishAsync(keys.EventsChannel, payload).WaitAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is RedisException or TimeoutException)
        {
            LogPublishFailed(logger, ex, arenaEvent.GetType().Name);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not publish live event {EventType}")]
    private static partial void LogPublishFailed(ILogger logger, Exception exception, string eventType);
}
