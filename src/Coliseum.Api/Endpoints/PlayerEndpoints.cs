using Coliseum.Api.Auth;
using Coliseum.Api.Middleware;
using Coliseum.Application.UseCases.Players;
using Coliseum.Contracts.Players;

namespace Coliseum.Api.Endpoints;

/// <summary>REQ-02: create and read players. Creation is a service operation; profiles are readable by any token.</summary>
public static class PlayerEndpoints
{
    public static IEndpointRouteBuilder MapPlayerEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/players").WithTags("Players");

        group.MapPost("/", async (CreatePlayerRequest request, CreatePlayerHandler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(request, cancellationToken))
                    .ToResult(created => Results.Created($"/players/{created.Player.Id}", created)))
            .RequireAuthorization(AuthPolicies.Service)
            .WithSummary("Create a player (service token). Returns the player and a player-scoped token.")
            .Produces<CreatePlayerResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet("/", async (int? limit, ListPlayersHandler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(limit, cancellationToken)).ToResult(Results.Ok))
            .RequireAuthorization(AuthPolicies.PlayerOrService)
            .WithSummary("Recent players, newest first (opponent discovery)")
            .Produces<IReadOnlyList<PlayerResponse>>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapGet("/{id}", async (string id, GetPlayerHandler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(id, cancellationToken)).ToResult(Results.Ok))
            .RequireAuthorization(AuthPolicies.PlayerOrService)
            .WithSummary("Read a player profile")
            .Produces<PlayerResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
