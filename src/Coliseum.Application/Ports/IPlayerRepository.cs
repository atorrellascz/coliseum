using Coliseum.Domain.Players;

namespace Coliseum.Application.Ports;

/// <summary>
/// Player persistence. Deliberately thin (PAT-02): access is by key, there are no generic queries.
/// Name uniqueness is the adapter's job because it needs global knowledge (an atomic SET NX in Redis).
/// </summary>
public interface IPlayerRepository
{
    /// <summary>Stores a new player. Returns false, without writing anything, when the normalized name is already taken.</summary>
    Task<bool> CreateAsync(Player player, CancellationToken cancellationToken);

    Task<Player?> GetAsync(PlayerId id, CancellationToken cancellationToken);

    /// <summary>Batch read in one round trip. Missing ids are simply absent from the result.</summary>
    Task<IReadOnlyDictionary<PlayerId, Player>> GetManyAsync(IReadOnlyCollection<PlayerId> ids, CancellationToken cancellationToken);
}
