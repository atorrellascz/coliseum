using System.ComponentModel.DataAnnotations;

namespace Coliseum.Worker.Options;

/// <summary>Worker tunables (section <c>Worker</c>). Defaults suit a single replica on a developer machine or one pod.</summary>
public sealed class WorkerOptions
{
    public const string SectionName = "Worker";

    /// <summary>Consumer name inside the group. Unique per process so pending entries can be attributed and reclaimed.</summary>
    public string ConsumerName { get; set; } = $"{Environment.MachineName}-{Guid.NewGuid():N}"[..Math.Min(40, Environment.MachineName.Length + 33)];

    /// <summary>Battles simulated in parallel. The work is I/O bound (one Redis round trip per battle), so 2x cores is plenty.</summary>
    [Range(1, 256)]
    public int MaxConcurrency { get; set; } = Environment.ProcessorCount * 2;

    /// <summary>How many new entries to pull per read.</summary>
    [Range(1, 1000)]
    public int ReadBatchSize { get; set; } = 32;

    /// <summary>Upper bound of the in-memory pending list; above it the worker stops reading until battles finish.</summary>
    [Range(1, 10_000)]
    public int MaxPending { get; set; } = 256;

    /// <summary>Sleep when there is nothing to do (the stream read is non-blocking, SUP-15).</summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromMilliseconds(250);

    /// <summary>How often to look for entries left pending by dead consumers.</summary>
    public TimeSpan ClaimInterval { get; set; } = TimeSpan.FromSeconds(15);

    /// <summary>A pending entry older than this is considered orphaned and reclaimed.</summary>
    public TimeSpan ClaimMinIdle { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>After this many deliveries a failing entry goes to the dead-letter stream.</summary>
    [Range(1, 100)]
    public int MaxDeliveries { get; set; } = 5;

    /// <summary>Time given to in-flight battles on SIGTERM before the process exits.</summary>
    public TimeSpan ShutdownGrace { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>How often queue statistics are refreshed for the gauges.</summary>
    public TimeSpan StatsInterval { get; set; } = TimeSpan.FromSeconds(5);
}
