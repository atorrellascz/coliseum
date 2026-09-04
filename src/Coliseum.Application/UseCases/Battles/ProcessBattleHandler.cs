using System.Diagnostics;
using Coliseum.Application.Ports;
using Coliseum.Application.Telemetry;
using Coliseum.Contracts.Battles;
using Coliseum.Contracts.Events;
using Coliseum.Domain.Battles;
using Microsoft.Extensions.Logging;

namespace Coliseum.Application.UseCases.Battles;

/// <summary>What happened to one queued message. The worker acknowledges on every outcome except <see cref="Failed"/>.</summary>
public enum ProcessOutcome
{
    /// <summary>Simulated and settled by this call.</summary>
    Processed,

    /// <summary>Already settled earlier (re-delivery after a crash); nothing changed.</summary>
    Duplicate,

    /// <summary>A participant no longer exists; the record is marked failed (SUP-08).</summary>
    PlayerMissing,

    /// <summary>The engine refused the battle (rules invariant); the record is marked failed.</summary>
    Failed,
}

/// <summary>
/// The worker's unit of work: load players, run the deterministic engine, settle atomically, publish, measure.
/// Infrastructure exceptions (Redis unreachable) are intentionally not caught here: the worker retries and
/// dead-letters after a delivery threshold, and the settlement's idempotency makes retries safe.
/// </summary>
public sealed partial class ProcessBattleHandler(
    IPlayerRepository players,
    IBattleReportStore reports,
    IBattleLedger ledger,
    IEventPublisher events,
    BattleRules rules,
    IClock clock,
    ILogger<ProcessBattleHandler> logger)
{
    public async Task<ProcessOutcome> HandleAsync(QueuedBattle message, CancellationToken cancellationToken)
    {
        using var activity = ColiseumTelemetry.ActivitySource.StartActivity("battle.process");
        activity?.SetTag("battle.id", message.BattleId.Value);
        long started = Stopwatch.GetTimestamp();

        var existing = await reports.GetAsync(message.BattleId, cancellationToken);
        if (existing is { Status: BattleStatus.Done })
        {
            return Finish(ProcessOutcome.Duplicate, message, started);
        }

        await reports.MarkProcessingAsync(message.BattleId, cancellationToken);

        var found = await players.GetManyAsync([message.AttackerId, message.DefenderId], cancellationToken);
        if (!found.TryGetValue(message.AttackerId, out var attacker) || !found.TryGetValue(message.DefenderId, out var defender))
        {
            return await FailAsync(ProcessOutcome.PlayerMissing, message, "player_missing", started, cancellationToken);
        }

        var run = BattleEngine.Run(message.BattleId, attacker, defender, rules);
        if (run.IsFailure)
        {
            return await FailAsync(ProcessOutcome.Failed, message, run.Errors[0].Code, started, cancellationToken);
        }

        var report = run.Value;
        var settlement = await ledger.ApplyAsync(report, cancellationToken);

        switch (settlement.Outcome)
        {
            case SettlementOutcome.AlreadyApplied:
                return Finish(ProcessOutcome.Duplicate, message, started);
            case SettlementOutcome.PlayerMissing:
                return await FailAsync(ProcessOutcome.PlayerMissing, message, "player_missing", started, cancellationToken);
            case SettlementOutcome.Applied:
                break;
            default:
                throw new InvalidOperationException("Unknown settlement outcome: " + settlement.Outcome);
        }

        var now = clock.UtcNow;
        ColiseumTelemetry.BattleTurns.Record(report.Turns);
        ColiseumTelemetry.QueueLatency.Record((now - message.SubmittedAt).TotalSeconds);
        ColiseumTelemetry.ResourcesStolen.Add(settlement.GoldTransferred, ColiseumTelemetry.GoldTag);
        ColiseumTelemetry.ResourcesStolen.Add(settlement.SilverTransferred, ColiseumTelemetry.SilverTag);

        LogBattleDone(logger, report.BattleId.Value, report.WinnerId.Value, report.LoserId.Value, report.Turns, settlement.Score);

        await events.PublishAsync(
            new BattleDoneEvent(
                now,
                report.BattleId.Value,
                report.AttackerId.Value,
                report.DefenderId.Value,
                report.WinnerId.Value,
                report.LoserId.Value,
                report.Turns,
                report.Loot.Percent,
                settlement.GoldTransferred,
                settlement.SilverTransferred,
                settlement.Score),
            cancellationToken);

        return Finish(ProcessOutcome.Processed, message, started);
    }

    private async Task<ProcessOutcome> FailAsync(ProcessOutcome outcome, QueuedBattle message, string error, long started, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        await reports.MarkFailedAsync(message.BattleId, error, now, cancellationToken);
        LogBattleFailed(logger, message.BattleId.Value, error);
        await events.PublishAsync(new BattleFailedEvent(now, message.BattleId.Value, message.AttackerId.Value, message.DefenderId.Value, error), cancellationToken);
        return Finish(outcome, message, started);
    }

    private static ProcessOutcome Finish(ProcessOutcome outcome, QueuedBattle message, long started)
    {
        ColiseumTelemetry.BattlesProcessed.Add(1, ColiseumTelemetry.ResultTag(outcome));
        ColiseumTelemetry.ProcessingDuration.Record(Stopwatch.GetElapsedTime(started).TotalSeconds);
        Activity.Current?.SetTag("battle.outcome", outcome.ToString());
        _ = message;
        return outcome;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Battle {BattleId} done: {WinnerId} beat {LoserId} in {Turns} turns, score {Score}")]
    private static partial void LogBattleDone(ILogger logger, string battleId, string winnerId, string loserId, int turns, long score);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Battle {BattleId} failed: {Reason}")]
    private static partial void LogBattleFailed(ILogger logger, string battleId, string reason);
}
