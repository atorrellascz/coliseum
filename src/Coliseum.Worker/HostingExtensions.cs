using Coliseum.Application;
using Coliseum.Application.Options;
using Coliseum.Infrastructure.Redis.Connection;
using Coliseum.Infrastructure.Redis.DependencyInjection;
using Coliseum.ServiceDefaults;
using Coliseum.Worker.Options;
using Coliseum.Worker.Processing;

namespace Coliseum.Worker;

/// <summary>Composition root of the battle processor (ADR-09: a web host, so Kubernetes can probe it and Prometheus scrape it).</summary>
public static class HostingExtensions
{
    public const string ServiceName = "coliseum-worker";

    public static WebApplicationBuilder AddColiseumWorker(this WebApplicationBuilder builder)
    {
        builder.AddServiceDefaults(ServiceName);

        builder.Services.Configure<RedisOptions>(builder.Configuration.GetSection(RedisOptions.SectionName));
        builder.Services.Configure<BattleRulesOptions>(builder.Configuration.GetSection(BattleRulesOptions.SectionName));
        builder.Services.AddOptions<WorkerOptions>()
            .Bind(builder.Configuration.GetSection(WorkerOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        builder.Services.AddColiseumRedis(clientName: ServiceName);
        builder.Services.AddColiseumProcessing();

        var workerOptions = builder.Configuration.GetSection(WorkerOptions.SectionName).Get<WorkerOptions>() ?? new WorkerOptions();
        builder.Services.AddBattleScheduler(workerOptions.MaxConcurrency);
        builder.Services.AddSingleton<WorkerHeartbeat>();
        builder.Services.AddSingleton<BattleExecutor>();
        builder.Services.AddHostedService<BattleProcessorService>();
        builder.Services.AddHealthChecks().AddCheck<WorkerHealthCheck>("worker", tags: [HealthEndpoints.ReadyTag]);

        return builder;
    }

    public static WebApplication UseColiseumWorker(this WebApplication app)
    {
        app.MapDefaultEndpoints();
        return app;
    }
}
