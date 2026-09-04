using System.ComponentModel;
using Coliseum.Contracts.Players;
using ModelContextProtocol.Server;

namespace Coliseum.Mcp.Tools;

/// <summary>Player management exposed to agents. Every tool goes through the API, so validation and auth are the API's.</summary>
[McpServerToolType]
public static class PlayerTools
{
    [McpServerTool(Name = "create_player")]
    [Description("Create a new player. Name must be unique (case-insensitive) and at most 20 characters; gold and silver at most 1,000,000,000; attack and hit points 1-10,000; defense 0-10,000. Returns the player and a player-scoped token.")]
    public static Task<CreatePlayerResponse> CreatePlayer(
        ColiseumApiClient api,
        [Description("Unique display name, max 20 characters")] string name,
        [Description("Attack value, 1-10000")] int attack,
        [Description("Defense value, 0-10000. Dodge chance is defense / (defense + attacker's attack), capped at 75%")] int defense,
        [Description("Hit points, 1-10000")] int hitPoints,
        [Description("Starting gold, 0-1000000000")] long gold = 500,
        [Description("Starting silver, 0-1000000000")] long silver = 120,
        [Description("Optional description, max 1000 characters")] string? description = null,
        CancellationToken cancellationToken = default) =>
        api.CreatePlayerAsync(new CreatePlayerRequest(name, description, gold, silver, attack, defense, hitPoints), cancellationToken);

    [McpServerTool(Name = "get_player")]
    [Description("Read a player's public profile: stats and current gold and silver.")]
    public static Task<PlayerResponse> GetPlayer(
        ColiseumApiClient api,
        [Description("Player id (26-character ULID)")] string playerId,
        CancellationToken cancellationToken = default) =>
        api.GetPlayerAsync(playerId, cancellationToken);
}
