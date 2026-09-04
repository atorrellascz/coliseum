// Composition root of the battle processor. Nothing here but wiring: options, adapters, use cases, the loop,
// health and metrics endpoints (ADR-09: the worker is a web host so Kubernetes can probe and Prometheus can scrape it).
using Coliseum.Application;
using Coliseum.Application.Options;
using Coliseum.Infrastructure.Redis.Connection;
using Coliseum.Infrastructure.Redis.DependencyInjection;
using Coliseum.ServiceDefaults;
using Coliseum.Worker.Options;
using Coliseum.Worker.Processing;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults("coliseum-worker");

builder.Services.Configure<RedisOptions>(builder.Configuration.GetSection(RedisOptions.SectionName));
builder.Services.Configure<BattleRulesOptions>(builder.Configuration.GetSection(BattleRulesOptions.SectionName));
builder.Services.AddOptions<WorkerOptions>().Bind(builder.Configuration.GetSection(WorkerOptions.SectionName)).ValidateDataAnnotations().ValidateOnStart();

builder.Services.AddColiseumRedis(clientName: "coliseum-worker");
builder.Services.AddColiseumProcessing();

var workerOptions = builder.Configuration.GetSection(WorkerOptions.SectionName).Get<WorkerOptions>() ?? new WorkerOptions();
builder.Services.AddBattleScheduler(workerOptions.MaxConcurrency);
builder.Services.AddSingleton<WorkerHeartbeat>();
builder.Services.AddSingleton<BattleExecutor>();
builder.Services.AddHostedService<BattleProcessorService>();
builder.Services.AddHealthChecks().AddCheck<WorkerHealthCheck>("worker", tags: [HealthEndpoints.ReadyTag]);

var app = builder.Build();
app.MapDefaultEndpoints();
app.Run();
