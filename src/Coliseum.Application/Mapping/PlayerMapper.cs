using Coliseum.Contracts.Players;
using Coliseum.Domain.Players;

namespace Coliseum.Application.Mapping;

/// <summary>Domain to contract mapping. Lives here because Contracts must not reference Domain.</summary>
public static class PlayerMapper
{
    public static PlayerResponse ToResponse(Player player) =>
        new(
            player.Id.Value,
            player.Name,
            player.Description,
            player.Resources.Gold,
            player.Resources.Silver,
            player.Stats.Attack,
            player.Stats.Defense,
            player.Stats.HitPoints,
            player.CreatedAt);
}
