using StackExchange.Redis;

namespace Coliseum.Infrastructure.Redis.Connection;

/// <summary>
/// Builds the one <see cref="ConnectionMultiplexer"/> of the process (PAT-03). StackExchange.Redis multiplexes
/// every command over a single connection and is thread-safe; creating one per request is the classic mistake.
/// <c>AbortOnConnectFail = false</c> lets the process start while Redis is still coming up (Kubernetes ordering)
/// and reconnect on its own.
/// </summary>
public static class RedisConnectionFactory
{
    public static ConfigurationOptions BuildConfiguration(RedisOptions options, string clientName)
    {
        var configuration = ConfigurationOptions.Parse(options.ConnectionString);
        configuration.AbortOnConnectFail = false;
        configuration.ConnectTimeout = options.ConnectTimeoutMs;
        configuration.SyncTimeout = options.SyncTimeoutMs;
        configuration.AsyncTimeout = options.SyncTimeoutMs;
        configuration.ConnectRetry = 3;
        configuration.ClientName = clientName;
        return configuration;
    }

    public static IConnectionMultiplexer Connect(RedisOptions options, string clientName) =>
        ConnectionMultiplexer.Connect(BuildConfiguration(options, clientName));
}
