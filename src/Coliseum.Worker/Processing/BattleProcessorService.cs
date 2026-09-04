using System.Threading.Channels;
using Coliseum.Application.Ports;
using Coliseum.Application.Scheduling;
using Coliseum.Application.Telemetry;
using Coliseum.Worker.Options;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Coliseum.Worker.Processing;

/// <summary>
/// The processing loop (single-writer, ARQ-03). One task owns the scheduler and the queue cursor:
/// <list type="number">
/// <item>collect completions from the channel: free players, XACK (or dead-letter after too many deliveries);</item>
/// <item>every <c>ClaimInterval</c>, reclaim entries left pending by dead consumers (XAUTOCLAIM);</item>
/// <item>read new entries while the in-memory pending list has room;</item>
/// <item>dispatch every battle the scheduler allows onto the thread pool;</item>
/// <item>when nothing happened, wait for a completion or <c>PollInterval</c>.</item>
/// </list>
/// XACK happens only after the settlement succeeded, so a crash anywhere before it leaves the entry pending and
/// another consumer reclaims it; the settlement's idempotency makes that safe (ADR-03).
/// </summary>
public sealed partial class BattleProcessorService(
    IBattleQueue queue,
    BattleScheduler scheduler,
    BattleExecutor executor,
    WorkerHeartbeat heartbeat,
    IOptions<WorkerOptions> options,
    TimeProvider timeProvider,
    ILogger<BattleProcessorService> logger) : BackgroundService
{
    private readonly WorkerOptions _options = options.Value;
    private readonly Channel<Completion> _completions = Channel.CreateUnbounded<Completion>(new UnboundedChannelOptions { SingleReader = true });
    private readonly CancellationTokenSource _executionCts = new();
    private readonly List<Task> _inFlight = [];
    private QueueStats _lastStats = new(0, 0, 0);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        RegisterGauges();
        await queue.InitializeAsync(stoppingToken);
        LogStarted(logger, _options.ConsumerName, _options.MaxConcurrency);

        DateTimeOffset lastClaim = DateTimeOffset.MinValue;
        DateTimeOffset lastStats = DateTimeOffset.MinValue;

        while (!stoppingToken.IsCancellationRequested)
        {
            heartbeat.Beat();
            bool progressed = false;

            try
            {
                progressed |= await DrainCompletionsAsync(stoppingToken);

                var now = timeProvider.GetUtcNow();
                if (now - lastClaim >= _options.ClaimInterval)
                {
                    progressed |= await ClaimStaleAsync(now, stoppingToken);
                    lastClaim = now;
                }

                if (scheduler.PendingCount < _options.MaxPending)
                {
                    var fresh = await queue.ReadAsync(_options.ConsumerName, _options.ReadBatchSize, TimeSpan.Zero, stoppingToken);
                    foreach (var message in fresh)
                    {
                        scheduler.Enqueue(message, now);
                    }

                    progressed |= fresh.Count > 0;
                }

                progressed |= Dispatch();

                if (now - lastStats >= _options.StatsInterval)
                {
                    _lastStats = await queue.GetStatsAsync(stoppingToken);
                    lastStats = now;
                }
            }
            catch (Exception ex) when (ex is RedisException or TimeoutException)
            {
                LogRedisTrouble(logger, ex);
                await Task.Delay(_options.PollInterval * 4, stoppingToken);
                continue;
            }

            if (!progressed)
            {
                await WaitForWorkAsync(stoppingToken);
            }
        }

        await ShutdownAsync();
    }

    private async Task<bool> DrainCompletionsAsync(CancellationToken cancellationToken)
    {
        bool any = false;
        while (_completions.Reader.TryRead(out var completion))
        {
            any = true;
            scheduler.Complete(completion.Battle.MessageId);
            var message = completion.Battle.Message;

            if (completion.Error is null)
            {
                await queue.AcknowledgeAsync(message.MessageId, cancellationToken);
                continue;
            }

            if (message.DeliveryCount >= _options.MaxDeliveries)
            {
                await queue.DeadLetterAsync(message, $"{completion.Error.GetType().Name}: {completion.Error.Message}", cancellationToken);
                LogDeadLettered(logger, message.BattleId.Value, message.DeliveryCount);
            }

            // Otherwise the entry stays pending and XAUTOCLAIM retries it after ClaimMinIdle.
        }

        return any;
    }

    private async Task<bool> ClaimStaleAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var claimed = await queue.ClaimStaleAsync(_options.ConsumerName, _options.ClaimMinIdle, _options.ReadBatchSize, cancellationToken);
        foreach (var message in claimed)
        {
            scheduler.Enqueue(message, now);
        }

        if (claimed.Count > 0)
        {
            LogClaimed(logger, claimed.Count);
        }

        return claimed.Count > 0;
    }

    private bool Dispatch()
    {
        var started = scheduler.Dispatch();
        foreach (var battle in started)
        {
            _inFlight.Add(RunAsync(battle));
        }

        _inFlight.RemoveAll(task => task.IsCompleted);
        return started.Count > 0;
    }

    private Task RunAsync(ScheduledBattle battle) =>
        Task.Run(async () =>
        {
            var completion = await executor.ExecuteAsync(battle, _executionCts.Token);
            await _completions.Writer.WriteAsync(completion, CancellationToken.None);
        });

    private async Task WaitForWorkAsync(CancellationToken stoppingToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        timeout.CancelAfter(_options.PollInterval);
        try
        {
            await _completions.Reader.WaitToReadAsync(timeout.Token);
        }
        catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
        {
            // Poll interval elapsed: loop again.
        }
    }

    /// <summary>Stop reading, let in-flight battles finish within the grace period, acknowledge what completed.</summary>
    private async Task ShutdownAsync()
    {
        int inFlight = _inFlight.Count(t => !t.IsCompleted);
        LogStopping(logger, inFlight);
        _executionCts.CancelAfter(_options.ShutdownGrace);

        try
        {
            await Task.WhenAll(_inFlight).WaitAsync(_options.ShutdownGrace);
        }
        catch (Exception ex) when (ex is TimeoutException or OperationCanceledException)
        {
            // Whatever did not finish stays pending in Redis and will be reclaimed by the next worker.
        }

        await DrainCompletionsAsync(CancellationToken.None);
    }

    private void RegisterGauges()
    {
        ColiseumTelemetry.Meter.CreateObservableGauge("coliseum.scheduler.running", () => scheduler.RunningCount, "{battle}", "Battles being simulated right now.");
        ColiseumTelemetry.Meter.CreateObservableGauge("coliseum.scheduler.pending", () => scheduler.PendingCount, "{battle}", "Battles read from the stream and waiting for a free player.");
        ColiseumTelemetry.Meter.CreateObservableGauge("coliseum.queue.length", () => _lastStats.Length, "{entry}", "Entries in the battle stream.");
        ColiseumTelemetry.Meter.CreateObservableGauge("coliseum.queue.pending", () => _lastStats.Pending, "{entry}", "Delivered but not yet acknowledged entries.");
        ColiseumTelemetry.Meter.CreateObservableGauge("coliseum.dlq.length", () => _lastStats.DeadLettered, "{entry}", "Entries in the dead-letter stream.");
    }

    public override void Dispose()
    {
        _executionCts.Dispose();
        base.Dispose();
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Battle processor started as consumer {Consumer} with concurrency {MaxConcurrency}")]
    private static partial void LogStarted(ILogger logger, string consumer, int maxConcurrency);

    [LoggerMessage(Level = LogLevel.Information, Message = "Reclaimed {Count} pending entries from idle consumers")]
    private static partial void LogClaimed(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Error, Message = "Battle {BattleId} dead-lettered after {DeliveryCount} deliveries")]
    private static partial void LogDeadLettered(ILogger logger, string battleId, int deliveryCount);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Redis unavailable; backing off")]
    private static partial void LogRedisTrouble(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Stopping; waiting for {InFlight} in-flight battles")]
    private static partial void LogStopping(ILogger logger, int inFlight);
}
