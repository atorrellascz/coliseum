using System.Diagnostics;
using System.Diagnostics.Metrics;
using Coliseum.Application.UseCases.Battles;

namespace Coliseum.Application.Telemetry;

/// <summary>
/// The single ActivitySource and Meter of the system (OPS-02). Hosts subscribe both to OpenTelemetry.
/// Cardinality rule: battle and player ids are never metric tags; they go to traces and logs.
/// Names follow OpenTelemetry conventions (dots); the Prometheus exporter renders them as <c>coliseum_battles_submitted_total</c> etc.
/// </summary>
public static class ColiseumTelemetry
{
    public const string ServiceName = "coliseum";

    public static ActivitySource ActivitySource { get; } = new(ServiceName);

    public static Meter Meter { get; } = new(ServiceName);

    public static Counter<long> BattlesSubmitted { get; } =
        Meter.CreateCounter<long>("coliseum.battles.submitted", "{battle}", "Battle requests accepted by the API.");

    /// <summary>Tag <c>result</c>: processed | duplicate | player_missing | failed. A rising duplicate count reveals re-deliveries.</summary>
    public static Counter<long> BattlesProcessed { get; } =
        Meter.CreateCounter<long>("coliseum.battles.processed", "{battle}", "Queued battles handled by the worker, by result.");

    public static Histogram<double> ProcessingDuration { get; } =
        Meter.CreateHistogram<double>("coliseum.battle.processing.duration", "s", "Time to load, simulate and settle one battle.");

    public static Histogram<double> QueueLatency { get; } =
        Meter.CreateHistogram<double>("coliseum.battle.queue.latency", "s", "Time from submission to settlement (the user-facing SLO).");

    public static Histogram<int> BattleTurns { get; } =
        Meter.CreateHistogram<int>("coliseum.battle.turns", "{turn}", "Turns per battle; a game-balance signal.");

    /// <summary>Tag <c>resource</c>: gold | silver.</summary>
    public static Counter<long> ResourcesStolen { get; } =
        Meter.CreateCounter<long>("coliseum.resources.stolen", "{unit}", "Resources transferred from losers to winners.");

    public static KeyValuePair<string, object?> GoldTag { get; } = new("resource", "gold");

    public static KeyValuePair<string, object?> SilverTag { get; } = new("resource", "silver");

    public static KeyValuePair<string, object?> ResultTag(ProcessOutcome outcome) =>
        new("result", outcome switch
        {
            ProcessOutcome.Processed => "processed",
            ProcessOutcome.Duplicate => "duplicate",
            ProcessOutcome.PlayerMissing => "player_missing",
            _ => "failed",
        });
}
