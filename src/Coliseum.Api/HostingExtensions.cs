using System.Text.Json;
using System.Text.Json.Serialization;
using Coliseum.Api.Auth;
using Coliseum.Api.Endpoints;
using Coliseum.Api.Hubs;
using Coliseum.Api.Middleware;
using Coliseum.Api.Options;
using Coliseum.Application;
using Coliseum.Application.Options;
using Coliseum.Infrastructure.Redis.Connection;
using Coliseum.Infrastructure.Redis.DependencyInjection;
using Coliseum.ServiceDefaults;
using Scalar.AspNetCore;

namespace Coliseum.Api;

/// <summary>
/// The API's composition root, split in two steps so <c>Program.cs</c> stays a five-line script:
/// <see cref="AddColiseumApi"/> registers services, <see cref="UseColiseumApi"/> orders the pipeline.
/// Every cross-cutting concern lives in its own file (auth, rate limiting, security headers, problem details);
/// this class only decides what is on and in which order.
/// </summary>
public static class HostingExtensions
{
    public const string ServiceName = "coliseum-api";

    public static WebApplicationBuilder AddColiseumApi(this WebApplicationBuilder builder)
    {
        builder.AddServiceDefaults(ServiceName);
        builder.ConfigureRequestLimits();

        builder.Services.Configure<RedisOptions>(builder.Configuration.GetSection(RedisOptions.SectionName));
        builder.Services.Configure<BattleRulesOptions>(builder.Configuration.GetSection(BattleRulesOptions.SectionName));

        builder.Services.AddColiseumRedis(clientName: ServiceName);
        builder.Services.AddColiseumApplication();
        builder.Services.AddColiseumAuth(builder.Configuration);
        builder.Services.AddColiseumRateLimiting(builder.Configuration);

        builder.Services.AddProblemDetails();
        builder.Services.AddOpenApi();
        builder.Services.ConfigureHttpJsonOptions(json =>
            json.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)));

        builder.Services.AddSignalR(signalr => signalr.EnableDetailedErrors = builder.Environment.IsDevelopment());
        builder.Services.AddHostedService<ArenaEventRelay>();

        return builder;
    }

    /// <summary>
    /// Middleware order matters and is documented here once:
    /// exceptions → security headers → static clients (public) → CORS → rate limit → authn → authz → endpoints.
    /// </summary>
    public static WebApplication UseColiseumApi(this WebApplication app)
    {
        app.UseExceptionHandler();
        app.UseSecurityHeaders();
        app.UseDefaultFiles();
        app.UseStaticFiles();
        app.UseCors(SecurityHeaders.CorsPolicy);
        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseAuthorization();

        app.MapDefaultEndpoints();
        app.MapOpenApi().AllowAnonymous();
        app.MapScalarApiReference(options => options.Title = "Coliseum API").AllowAnonymous();

        app.MapAuthEndpoints();
        app.MapPlayerEndpoints();
        app.MapBattleEndpoints();
        app.MapLeaderboardEndpoints();
        app.MapHub<ArenaHub>("/hubs/arena");

        return app;
    }
}
