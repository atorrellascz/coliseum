// Composition root of the HTTP API. Only wiring lives here: options, adapters, use cases, auth, limits, endpoints.
using System.Text.Json.Serialization;
using Coliseum.Api.Auth;
using Coliseum.Api.Endpoints;
using Coliseum.Api.Middleware;
using Coliseum.Api.Options;
using Coliseum.Application;
using Coliseum.Application.Options;
using Coliseum.Infrastructure.Redis.Connection;
using Coliseum.Infrastructure.Redis.DependencyInjection;
using Coliseum.ServiceDefaults;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults("coliseum-api");
builder.ConfigureRequestLimits();

builder.Services.Configure<RedisOptions>(builder.Configuration.GetSection(RedisOptions.SectionName));
builder.Services.Configure<BattleRulesOptions>(builder.Configuration.GetSection(BattleRulesOptions.SectionName));

builder.Services.AddColiseumRedis(clientName: "coliseum-api");
builder.Services.AddColiseumApplication();
builder.Services.AddColiseumAuth(builder.Configuration);
builder.Services.AddColiseumRateLimiting(builder.Configuration);

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.ConfigureHttpJsonOptions(json =>
    json.SerializerOptions.Converters.Add(new JsonStringEnumConverter(System.Text.Json.JsonNamingPolicy.CamelCase)));

var app = builder.Build();

app.UseExceptionHandler();
app.UseSecurityHeaders();
app.UseCors(SecurityHeaders.CorsPolicy);
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapDefaultEndpoints();
app.MapOpenApi().AllowAnonymous();
app.MapScalarApiReference(options => options.Title = "Coliseum API").AllowAnonymous();

app.MapAuthEndpoints();
app.MapPlayerEndpoints();
app.MapBattleEndpoints();
app.MapLeaderboardEndpoints();

app.Run();
