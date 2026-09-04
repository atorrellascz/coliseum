using System.ComponentModel.DataAnnotations;

namespace Coliseum.Infrastructure.Redis.Connection;

/// <summary>
/// Redis settings (section <c>Redis</c>). The connection string uses StackExchange.Redis syntax
/// (<c>host:port,password=...,ssl=true</c>); the environment variable <c>REDIS_URL</c> overrides it in the hosts.
/// </summary>
public sealed class RedisOptions
{
    public const string SectionName = "Redis";

    [Required]
    public string ConnectionString { get; set; } = "localhost:6379";

    /// <summary>Prefix of every key, so several environments can share one Redis in development.</summary>
    [Required]
    [RegularExpression("^[A-Za-z0-9_-]+$")]
    public string KeyPrefix { get; set; } = "coliseum";

    /// <summary>Consumer group of the battle stream. One group = one logical set of workers.</summary>
    [Required]
    public string ConsumerGroup { get; set; } = "workers";

    /// <summary>Approximate cap of the battle stream (XADD MAXLEN ~). Old, acknowledged entries are trimmed.</summary>
    [Range(1_000, 100_000_000)]
    public long StreamMaxLength { get; set; } = 1_000_000;

    [Range(500, 60_000)]
    public int ConnectTimeoutMs { get; set; } = 5_000;

    [Range(500, 60_000)]
    public int SyncTimeoutMs { get; set; } = 2_000;
}
