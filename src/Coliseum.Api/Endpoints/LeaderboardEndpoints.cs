using Coliseum.Api.Auth;
using Coliseum.Api.Middleware;
using Coliseum.Application.UseCases.Leaderboard;
using Coliseum.Contracts.Leaderboard;

namespace Coliseum.Api.Endpoints;

/// <summary>REQ-04: ranked players with rank, score and player id; offset/limit paging capped at 100.</summary>
public static class LeaderboardEndpoints
{
    public static IEndpointRouteBuilder MapLeaderboardEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/leaderboard", async (int? offset, int? limit, GetLeaderboardHandler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(offset, limit, cancellationToken)).ToResult(Results.Ok))
            .RequireAuthorization(AuthPolicies.PlayerOrService)
            .WithTags("Leaderboard")
            .WithSummary("Ranked list of players by resources stolen")
            .Produces<LeaderboardResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        return app;
    }
}
