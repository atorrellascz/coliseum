using Coliseum.Application.Mapping;
using Coliseum.Application.Ports;
using Coliseum.Contracts.Battles;
using Coliseum.Domain.Battles;
using Coliseum.Domain.Common;
using Coliseum.Domain.Players;

namespace Coliseum.Application.UseCases.Battles;

/// <summary>
/// <c>GET /battles/{id}</c>. Participants and service tokens may read a battle. Anyone else gets 404, not 403,
/// so the endpoint does not reveal that the battle exists.
/// </summary>
public sealed class GetBattleHandler(IBattleReportStore reports, IPlayerRepository players)
{
    private static readonly DomainError NotFound = DomainError.NotFound("battle.not_found", "Battle not found.");

    public async Task<Result<BattleReportResponse>> HandleAsync(Caller caller, string? battleId, CancellationToken cancellationToken)
    {
        var id = BattleId.Create(battleId);
        if (id.IsFailure)
        {
            return Result.Fail<BattleReportResponse>(NotFound);
        }

        var record = await reports.GetAsync(id.Value, cancellationToken);
        if (record is null || (!caller.IsService && !(caller.PlayerId is { } playerId && record.Involves(playerId))))
        {
            return Result.Fail<BattleReportResponse>(NotFound);
        }

        var names = await ResolveNamesAsync(record, cancellationToken);
        return Result.Ok(BattleMapper.ToResponse(record, names));
    }

    private async Task<IReadOnlyDictionary<PlayerId, string>> ResolveNamesAsync(BattleRecord record, CancellationToken cancellationToken)
    {
        if (record.Report is null)
        {
            return new Dictionary<PlayerId, string>();
        }

        var found = await players.GetManyAsync([record.AttackerId, record.DefenderId], cancellationToken);
        return found.ToDictionary(pair => pair.Key, pair => pair.Value.Name);
    }
}
