using Coliseum.Application.Mapping;
using Coliseum.Application.Ports;
using Coliseum.Contracts.Players;
using Coliseum.Domain.Common;
using Coliseum.Domain.Players;

namespace Coliseum.Application.UseCases.Players;

/// <summary><c>GET /players/{id}</c>. Profiles are visible to any authenticated caller: the mini-game shows opponents.</summary>
public sealed class GetPlayerHandler(IPlayerRepository players)
{
    public async Task<Result<PlayerResponse>> HandleAsync(string? playerId, CancellationToken cancellationToken)
    {
        var id = PlayerId.Create(playerId);
        if (id.IsFailure)
        {
            return Result.Fail<PlayerResponse>(id.Errors);
        }

        var player = await players.GetAsync(id.Value, cancellationToken);

        return player is null
            ? Result.Fail<PlayerResponse>(DomainError.NotFound("player.not_found", "Player not found."))
            : Result.Ok(PlayerMapper.ToResponse(player));
    }
}
