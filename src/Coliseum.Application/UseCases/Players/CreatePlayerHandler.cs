using Coliseum.Application.Mapping;
using Coliseum.Application.Ports;
using Coliseum.Contracts.Players;
using Coliseum.Domain.Common;
using Coliseum.Domain.Players;
using Microsoft.Extensions.Logging;

namespace Coliseum.Application.UseCases.Players;

/// <summary>
/// <c>POST /players</c>: validate through the aggregate, persist with atomic name uniqueness, hand back a player token.
/// Endpoint-level authorization (service tokens only) is the host's responsibility.
/// </summary>
public sealed partial class CreatePlayerHandler(
    IPlayerRepository players,
    IIdGenerator ids,
    IClock clock,
    IAuthTokenService tokens,
    ILogger<CreatePlayerHandler> logger)
{
    public async Task<Result<CreatePlayerResponse>> HandleAsync(CreatePlayerRequest request, CancellationToken cancellationToken)
    {
        var id = PlayerId.Unchecked(ids.NewId());

        var player = Player.Create(
            id,
            request.Name,
            request.Description,
            request.Gold,
            request.Silver,
            request.Attack,
            request.Defense,
            request.HitPoints,
            clock.UtcNow);

        if (player.IsFailure)
        {
            return Result.Fail<CreatePlayerResponse>(player.Errors);
        }

        bool created = await players.CreateAsync(player.Value, cancellationToken);
        if (!created)
        {
            return Result.Fail<CreatePlayerResponse>(DomainError.Conflict("player.name.taken", "A player with this name already exists.", "name"));
        }

        var token = tokens.Issue(Caller.ForPlayer(id));
        LogPlayerCreated(logger, id.Value);

        return Result.Ok(new CreatePlayerResponse(PlayerMapper.ToResponse(player.Value), token.AccessToken, token.ExpiresAt));
    }

    // Source-generated logging: zero allocations when the level is off, and the message template is checked at compile time.
    [LoggerMessage(Level = LogLevel.Information, Message = "Player {PlayerId} created")]
    private static partial void LogPlayerCreated(ILogger logger, string playerId);
}
