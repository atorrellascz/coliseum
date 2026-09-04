namespace Coliseum.Worker;

/// <summary>
/// Anchor type for <c>WebApplicationFactory&lt;WorkerAssemblyMarker&gt;</c> in integration tests. The API and the
/// worker both have a top-level <c>Program</c>, which would be ambiguous from a test project referencing both.
/// </summary>
public sealed class WorkerAssemblyMarker;
