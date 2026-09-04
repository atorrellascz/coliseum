namespace Coliseum.Contracts.Battles;

/// <summary>Lifecycle of a battle request. Stored as text in Redis, so the names are part of the contract.</summary>
public enum BattleStatus
{
    /// <summary>Accepted and waiting in the stream.</summary>
    Queued,

    /// <summary>A worker picked it up.</summary>
    Processing,

    /// <summary>Simulated and settled; the report is available.</summary>
    Done,

    /// <summary>Could not be processed (missing player, rules invariant, poison message). See <c>Error</c>.</summary>
    Failed,
}
