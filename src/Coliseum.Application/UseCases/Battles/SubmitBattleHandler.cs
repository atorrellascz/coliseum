using Coliseum.Application.Ports;
using Coliseum.Application.Telemetry;
using Coliseum.Contracts.Battles;
using Coliseum.Contracts.Events;
using Coliseum.Domain.Battles;
using Coliseum.Domain.Common;
using Coliseum.Domain.Players;
using Microsoft.Extensions.Logging;

namespace Coliseum.Application.UseCases.Battles;

/// <summary>
/// <c>POST /battles</c>. Decides who the attacker is from the caller, validates both players in one read,
/// writes the battle record first and enqueues second (PAT-10), then answers 202: processing is asynchronous.
/// </summary>
public sealed partial class SubmitBattleHandler(
    IPlayerRepository players,
    IBattleReportStore reports,
    IBattleQueue queue,
    IEventPublisher events,
    IIdGenerator ids,
    IClock clock,
    ILogger<SubmitBattleHandler> logger)
{
    public async Task<Result<BattleSubmittedResponse>> HandleAsync(Caller caller, SubmitBattleRequest request, CancellationToken cancellationToken)
    {
        var participants = ResolveParticipants(caller, request);
        if (participants.IsFailure)
        {
            return Result.Fail<BattleSubmittedResponse>(participants.Errors);
        }

        var (attackerId, defenderId) = participants.Value;

        var found = await players.GetManyAsync([attackerId, defenderId], cancellationToken);
        var missing = new List<DomainError>(2);
        if (!found.ContainsKey(attackerId))
        {
            missing.Add(DomainError.NotFound("player.not_found", "Attacker not found."));
        }

        if (!found.ContainsKey(defenderId))
        {
            missing.Add(DomainError.NotFound("player.not_found", "Defender not found."));
        }

        if (missing.Count > 0)
        {
            return Result.Fail<BattleSubmittedResponse>(missing);
        }

        var battleId = BattleId.Unchecked(ids.NewId());
        var now = clock.UtcNow;

        // Record first, message second: a crash in between leaves a Queued record without a message, which is
        // visible and recoverable; the opposite (a message without a record) would be a battle nobody can look up.
        await reports.CreateQueuedAsync(battleId, attackerId, defenderId, now, cancellationToken);
        await queue.EnqueueAsync(battleId, attackerId, defenderId, now, cancellationToken);

        ColiseumTelemetry.BattlesSubmitted.Add(1);
        LogBattleQueued(logger, battleId.Value, attackerId.Value, defenderId.Value);

        await events.PublishAsync(new BattleQueuedEvent(now, battleId.Value, attackerId.Value, defenderId.Value), cancellationToken);

        return Result.Ok(new BattleSubmittedResponse(battleId.Value, BattleStatus.Queued, now));
    }

    /// <summary>
    /// A player token attacks as itself and may not name anyone else; a service token must name the attacker.
    /// Self-battles are rejected here so they never reach the queue.
    /// </summary>
    private static Result<(PlayerId Attacker, PlayerId Defender)> ResolveParticipants(Caller caller, SubmitBattleRequest request)
    {
        var errors = new List<DomainError>();

        var defender = PlayerId.Create(request.DefenderId);
        if (defender.IsFailure)
        {
            errors.Add(DomainError.Validation("defenderId", "battle.defender.invalid", "Defender id is required and must be a valid id."));
        }

        PlayerId? attacker = null;
        if (caller.IsService)
        {
            var requested = PlayerId.Create(request.AttackerId);
            if (requested.IsFailure)
            {
                errors.Add(DomainError.Validation("attackerId", "battle.attacker.required", "A service token must specify the attacker id."));
            }
            else
            {
                attacker = requested.Value;
            }
        }
        else
        {
            attacker = caller.PlayerId;
            if (request.AttackerId is not null && !caller.Is(PlayerId.Unchecked(request.AttackerId)))
            {
                return Result.Fail<(PlayerId, PlayerId)>(DomainError.Forbidden("battle.attacker.mismatch", "A player can only start battles as themselves."));
            }
        }

        if (errors.Count > 0)
        {
            return Result.Fail<(PlayerId, PlayerId)>(errors);
        }

        if (attacker!.Value == defender.Value)
        {
            return Result.Fail<(PlayerId, PlayerId)>(DomainError.Validation("defenderId", "battle.self", "A player cannot battle themselves."));
        }

        return Result.Ok((attacker.Value, defender.Value));
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Battle {BattleId} queued: {AttackerId} -> {DefenderId}")]
    private static partial void LogBattleQueued(ILogger logger, string battleId, string attackerId, string defenderId);
}
