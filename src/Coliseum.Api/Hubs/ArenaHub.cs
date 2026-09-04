using Coliseum.Api.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Coliseum.Api.Hubs;

/// <summary>
/// Live channel for clients (ADR-0010). A connection is placed in its own player's group straight from the
/// token on connect, so nobody can subscribe to someone else's battles. Service tokens may join the
/// back-office group (every event) or watch a specific player. The hub itself pushes nothing: the
/// <see cref="ArenaEventRelay"/> does, from the Redis channel the worker publishes to.
/// </summary>
[Authorize(Policy = AuthPolicies.PlayerOrService)]
public sealed class ArenaHub : Hub
{
    public const string EventMethod = "arenaEvent";
    public const string BackOfficeGroup = "backoffice";

    public static string PlayerGroup(string playerId) => "player:" + playerId;

    public override async Task OnConnectedAsync()
    {
        var caller = Context.User!.ToCaller();
        if (caller.PlayerId is { } playerId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, PlayerGroup(playerId.Value));
        }

        await base.OnConnectedAsync();
    }

    /// <summary>Back-office clients receive every event. Service tokens only.</summary>
    public Task JoinBackOffice()
    {
        RequireService();
        return Groups.AddToGroupAsync(Context.ConnectionId, BackOfficeGroup);
    }

    /// <summary>Follow one player's battles (a spectator view). Service tokens only; players are auto-subscribed to themselves.</summary>
    public Task WatchPlayer(string playerId)
    {
        RequireService();
        return Groups.AddToGroupAsync(Context.ConnectionId, PlayerGroup(playerId));
    }

    private void RequireService()
    {
        if (!Context.User!.ToCaller().IsService)
        {
            throw new HubException("A service token is required.");
        }
    }
}
