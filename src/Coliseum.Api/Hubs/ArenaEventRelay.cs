using System.Text.Json;
using Coliseum.Contracts.Events;
using Coliseum.Infrastructure.Redis.Keys;
using Coliseum.Infrastructure.Redis.Serialization;
using Microsoft.AspNetCore.SignalR;
using StackExchange.Redis;

namespace Coliseum.Api.Hubs;

/// <summary>
/// Subscribes to the Redis <c>arena:events</c> channel and forwards each event to the SignalR groups that should
/// see it. Every API replica subscribes on its own, so no SignalR backplane is needed: each replica delivers to
/// the connections it holds. The raw JSON is forwarded untouched, so the client sees exactly the contract the
/// worker published (with the <c>type</c> discriminator).
/// </summary>
public sealed partial class ArenaEventRelay(
    IConnectionMultiplexer redis,
    RedisKeys keys,
    IHubContext<ArenaHub> hub,
    ILogger<ArenaEventRelay> logger) : BackgroundService
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(2);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var subscriber = redis.GetSubscriber();
        string channelName = keys.EventsChannel.ToString();

        // Redis may still be starting when the API starts (Kubernetes launches pods in parallel). A failed
        // subscription must not stop the host: retry until it works or we are shutting down.
        while (true)
        {
            try
            {
                await subscriber.SubscribeAsync(keys.EventsChannel, (channel, payload) => ForwardAsync(payload, stoppingToken).ConfigureAwait(false));
                break;
            }
            catch (Exception ex) when (ex is RedisException or TimeoutException)
            {
                LogSubscribeRetry(logger, ex, channelName);
                await Task.Delay(RetryDelay, stoppingToken);
            }
        }

        LogSubscribed(logger, channelName);

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Shutting down.
        }

        await subscriber.UnsubscribeAsync(keys.EventsChannel);
    }

    private async Task ForwardAsync(RedisValue payload, CancellationToken cancellationToken)
    {
        string json = (string)payload!;
        ArenaEvent? arenaEvent;
        try
        {
            arenaEvent = JsonSerializer.Deserialize(json, ColiseumJsonContext.Default.ArenaEvent);
        }
        catch (JsonException ex)
        {
            LogBadEvent(logger, ex);
            return;
        }

        if (arenaEvent is null)
        {
            return;
        }

        try
        {
            if (arenaEvent.Recipients.Count == 0)
            {
                await hub.Clients.All.SendAsync(ArenaHub.EventMethod, json, cancellationToken);
                return;
            }

            var groups = arenaEvent.Recipients.Select(ArenaHub.PlayerGroup).Append(ArenaHub.BackOfficeGroup).Distinct().ToList();
            await hub.Clients.Groups(groups).SendAsync(ArenaHub.EventMethod, json, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogForwardFailed(logger, ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Relaying live events from {Channel}")]
    private static partial void LogSubscribed(ILogger logger, string channel);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Cannot subscribe to {Channel} yet; retrying")]
    private static partial void LogSubscribeRetry(ILogger logger, Exception exception, string channel);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Ignoring malformed live event")]
    private static partial void LogBadEvent(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not forward live event to clients")]
    private static partial void LogForwardFailed(ILogger logger, Exception exception);
}
