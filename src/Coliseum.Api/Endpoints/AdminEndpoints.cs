using Coliseum.Api.Auth;
using Coliseum.Application.UseCases.Admin;
using Coliseum.Contracts.Admin;

namespace Coliseum.Api.Endpoints;

/// <summary>Back-office data (service tokens only): economy counters, queue USE numbers, top of the leaderboard.</summary>
public static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/admin/stats", async (GetAdminStatsHandler handler, CancellationToken cancellationToken) =>
                Results.Ok(await handler.HandleAsync(cancellationToken)))
            .RequireAuthorization(AuthPolicies.Service)
            .WithTags("Admin")
            .WithSummary("Economy, queue and leaderboard snapshot for the back-office")
            .Produces<AdminStatsResponse>();

        return app;
    }
}
