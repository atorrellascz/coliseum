namespace Coliseum.Application.Ports;

/// <summary>
/// Factory for new identifiers (PAT-04). Production yields ULIDs: 26 characters, time-sortable, URL-safe and
/// within the <c>[A-Za-z0-9_-]</c> rule the domain enforces. Tests use predictable sequences.
/// </summary>
public interface IIdGenerator
{
    string NewId();
}
