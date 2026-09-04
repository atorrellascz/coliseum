using Coliseum.Application.Mapping;
using Coliseum.Application.Ports;
using Coliseum.Contracts.Players;
using Coliseum.Domain.Common;

namespace Coliseum.Application.UseCases.Players;

/// <summary><c>GET /players?limit=</c>: recent players for opponent discovery (the arena client and agents need someone to fight).</summary>
public sealed class ListPlayersHandler(IPlayerRepository players)
{
    public const int DefaultLimit = 50;
    public const int MaxLimit = 100;

    public async Task<Result<IReadOnlyList<PlayerResponse>>> HandleAsync(int? limit, CancellationToken cancellationToken)
    {
        int effectiveLimit = limit ?? DefaultLimit;
        if (effectiveLimit is < 1 or > MaxLimit)
        {
            return Result.Fail<IReadOnlyList<PlayerResponse>>(DomainError.Validation("limit", "players.limit.invalid", "Limit must be between 1 and 100."));
        }

        var recent = await players.ListRecentAsync(effectiveLimit, cancellationToken);
        return Result.Ok<IReadOnlyList<PlayerResponse>>(recent.Select(PlayerMapper.ToResponse).ToList());
    }
}
