using Coliseum.Application.Scheduling;
using Coliseum.Application.UseCases.Battles;

namespace Coliseum.Worker.Processing;

/// <summary>Result of running one battle on the thread pool. <see cref="Error"/> is set when infrastructure threw.</summary>
public sealed record Completion(ScheduledBattle Battle, ProcessOutcome? Outcome, Exception? Error);

/// <summary>
/// Runs <see cref="ProcessBattleHandler"/> for one message inside its own DI scope and converts any exception
/// into a <see cref="Completion"/>, so the scheduler loop never dies because of a single battle.
/// </summary>
public sealed partial class BattleExecutor(IServiceScopeFactory scopes, ILogger<BattleExecutor> logger)
{
    public async Task<Completion> ExecuteAsync(ScheduledBattle battle, CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopes.CreateScope();
            var handler = scope.ServiceProvider.GetRequiredService<ProcessBattleHandler>();
            var outcome = await handler.HandleAsync(battle.Message, cancellationToken);
            return new Completion(battle, outcome, null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogBattleThrew(logger, ex, battle.Message.BattleId.Value, battle.Message.DeliveryCount);
            return new Completion(battle, null, ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Battle {BattleId} threw on delivery {DeliveryCount}; it stays pending for retry")]
    private static partial void LogBattleThrew(ILogger logger, Exception exception, string battleId, int deliveryCount);
}
