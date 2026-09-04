using System.Security.Cryptography;
using System.Text;
using Coliseum.Application;
using Coliseum.Application.Options;
using Coliseum.Application.Ports;
using Coliseum.Contracts.Auth;
using Microsoft.Extensions.Options;

namespace Coliseum.Api.Auth;

/// <summary>
/// <c>POST /auth/token</c>: exchanges an API key (header <c>X-Api-Key</c>) for a service token. Keys are compared
/// in constant time so response timing reveals nothing about how many leading bytes matched.
/// </summary>
public static class ApiKeyExchange
{
    public const string HeaderName = "X-Api-Key";

    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/auth/token", (HttpRequest request, IOptions<AuthOptions> options, IAuthTokenService tokens) =>
            {
                string? presented = request.Headers[HeaderName].FirstOrDefault();
                if (presented is null || !options.Value.ApiKeys.Any(key => FixedTimeEquals(key, presented)))
                {
                    return Results.Unauthorized();
                }

                var issued = tokens.Issue(Caller.Service);
                return Results.Ok(new TokenResponse(issued.AccessToken, issued.ExpiresAt, AuthPolicies.RoleService));
            })
            .AllowAnonymous()
            .WithTags("Auth")
            .WithSummary("Exchange an API key for a service bearer token")
            .Produces<TokenResponse>()
            .Produces(StatusCodes.Status401Unauthorized);

        return app;
    }

    private static bool FixedTimeEquals(string expected, string presented) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(presented));
}
