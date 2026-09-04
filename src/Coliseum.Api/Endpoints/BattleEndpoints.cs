using System.Security.Claims;
using Coliseum.Api.Auth;
using Coliseum.Api.Middleware;
using Coliseum.Application.UseCases.Battles;
using Coliseum.Contracts.Battles;

namespace Coliseum.Api.Endpoints;

/// <summary>REQ-03: submit a battle (202, processed asynchronously) and read its report.</summary>
public static class BattleEndpoints
{
    public static IEndpointRouteBuilder MapBattleEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/battles").WithTags("Battles").RequireAuthorization(AuthPolicies.PlayerOrService);

        group.MapPost("/", async (SubmitBattleRequest request, ClaimsPrincipal user, SubmitBattleHandler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(user.ToCaller(), request, cancellationToken))
                    .ToResult(accepted => Results.Accepted($"/battles/{accepted.BattleId}", accepted)))
            .WithSummary("Queue a battle against an opponent. Player tokens attack as themselves.")
            .Produces<BattleSubmittedResponse>(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/{id}", async (string id, ClaimsPrincipal user, GetBattleHandler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(user.ToCaller(), id, cancellationToken)).ToResult(Results.Ok))
            .WithSummary("Read a battle: status while queued, full report with narrative once done")
            .Produces<BattleReportResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
