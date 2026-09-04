using Coliseum.Application;
using Coliseum.Application.Options;
using Coliseum.Application.Ports;
using Coliseum.Infrastructure.Redis.Connection;
using Coliseum.Infrastructure.Redis.DependencyInjection;
using Coliseum.Infrastructure.Redis.Keys;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using Testcontainers.Redis;

namespace Coliseum.IntegrationTests.Fixtures;

/// <summary>
/// One Redis 7 container for the whole test assembly (Testcontainers), or an existing server when <c>REDIS_URL</c>
/// is set (CI service container). Every test gets its own key prefix, so tests run in parallel without flushing.
/// </summary>
public sealed class RedisFixture : IAsyncLifetime
{
    private RedisContainer? _container;

    public string ConnectionString { get; private set; } = string.Empty;

    public IConnectionMultiplexer Multiplexer { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        string? external = Environment.GetEnvironmentVariable("REDIS_URL");
        if (!string.IsNullOrWhiteSpace(external))
        {
            ConnectionString = external;
        }
        else
        {
            _container = new RedisBuilder("redis:7-alpine").Build();
            await _container.StartAsync();
            ConnectionString = _container.GetConnectionString();
        }

        Multiplexer = await ConnectionMultiplexer.ConnectAsync(RedisConnectionFactory.BuildConfiguration(
            new RedisOptions { ConnectionString = ConnectionString }, "coliseum-tests"));
    }

    public async ValueTask DisposeAsync()
    {
        await Multiplexer.DisposeAsync();
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }

    /// <summary>A unique prefix per test so keys never collide across parallel tests.</summary>
    public static string NewPrefix() => "t" + Guid.NewGuid().ToString("N")[..12];

    /// <summary>Builds the real adapter graph (as the hosts do) against this Redis under <paramref name="prefix"/>.</summary>
    public ServiceProvider BuildServices(string prefix)
    {
        var services = new ServiceCollection();
        services.AddLogging(logging => logging.SetMinimumLevel(LogLevel.Warning));
        services.Configure<RedisOptions>(options =>
        {
            options.ConnectionString = ConnectionString;
            options.KeyPrefix = prefix;
        });
        services.Configure<BattleRulesOptions>(_ => { });
        services.AddSingleton(Multiplexer);
        services.AddSingleton<IAuthTokenService, NoTokenService>(); // only the API host issues tokens
        services.AddColiseumRedis(clientName: "coliseum-tests");
        services.AddColiseumApplication();
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }

    private sealed class NoTokenService : IAuthTokenService
    {
        public IssuedToken Issue(Caller caller) => new("no-token", DateTimeOffset.MaxValue);
    }

    public static RedisKeys KeysFor(string prefix) => new(prefix);
}

[CollectionDefinition(Name)]
public sealed class RedisCollection : ICollectionFixture<RedisFixture>
{
    public const string Name = "redis";
}
