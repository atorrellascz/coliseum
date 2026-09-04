using Coliseum.Application.Ports;
using Coliseum.Infrastructure.Redis.Adapters;
using Coliseum.Infrastructure.Redis.Connection;
using Coliseum.Infrastructure.Redis.Health;
using Coliseum.Infrastructure.Redis.Keys;
using Coliseum.Infrastructure.Redis.Scripts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Coliseum.Infrastructure.Redis.DependencyInjection;

/// <summary>Registers one adapter per port plus the shared multiplexer, key schema, scripts and health check.</summary>
public static class RedisServiceCollectionExtensions
{
    public static IServiceCollection AddColiseumRedis(this IServiceCollection services, string clientName)
    {
        services.AddOptions<RedisOptions>().ValidateDataAnnotations().ValidateOnStart();

        services.TryAddSingleton<IConnectionMultiplexer>(provider =>
            RedisConnectionFactory.Connect(provider.GetRequiredService<IOptions<RedisOptions>>().Value, clientName));
        services.TryAddSingleton(provider => new RedisKeys(provider.GetRequiredService<IOptions<RedisOptions>>().Value.KeyPrefix));
        services.TryAddSingleton(_ => LuaScripts.Load());
        services.TryAddSingleton(TimeProvider.System);

        services.TryAddSingleton<IClock, SystemClock>();
        services.TryAddSingleton<IIdGenerator, UlidIdGenerator>();
        services.TryAddSingleton<IPlayerRepository, RedisPlayerRepository>();
        services.TryAddSingleton<IBattleQueue, RedisBattleQueue>();
        services.TryAddSingleton<IBattleLedger, RedisBattleLedger>();
        services.TryAddSingleton<IBattleReportStore, RedisBattleReportStore>();
        services.TryAddSingleton<ILeaderboard, RedisLeaderboard>();
        services.TryAddSingleton<IEventPublisher, RedisEventPublisher>();

        services.AddHealthChecks().AddCheck<RedisHealthCheck>("redis", tags: ["ready"]);

        return services;
    }
}
