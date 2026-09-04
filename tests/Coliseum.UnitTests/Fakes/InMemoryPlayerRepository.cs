using Coliseum.Application.Ports;
using Coliseum.Domain.Players;

namespace Coliseum.UnitTests.Fakes;

/// <summary>Dictionary-backed repository honouring the same uniqueness contract as the Redis adapter (normalized name).</summary>
internal sealed class InMemoryPlayerRepository(List<string>? log = null) : IPlayerRepository
{
    private readonly Dictionary<PlayerId, Player> _players = [];
    private readonly HashSet<string> _names = new(StringComparer.Ordinal);

    public IReadOnlyDictionary<PlayerId, Player> All => _players;

    public Task<bool> CreateAsync(Player player, CancellationToken cancellationToken)
    {
        log?.Add("players:create");
        if (!_names.Add(player.NormalizedName))
        {
            return Task.FromResult(false);
        }

        _players[player.Id] = player;
        return Task.FromResult(true);
    }

    public Task<Player?> GetAsync(PlayerId id, CancellationToken cancellationToken) =>
        Task.FromResult(_players.GetValueOrDefault(id));

    public Task<IReadOnlyDictionary<PlayerId, Player>> GetManyAsync(IReadOnlyCollection<PlayerId> ids, CancellationToken cancellationToken)
    {
        IReadOnlyDictionary<PlayerId, Player> found = ids.Distinct().Where(_players.ContainsKey).ToDictionary(id => id, id => _players[id]);
        return Task.FromResult(found);
    }

    /// <summary>Test seam: store a player without going through Create (no uniqueness bookkeeping needed for ids).</summary>
    public void Seed(Player player)
    {
        _players[player.Id] = player;
        _names.Add(player.NormalizedName);
    }

    /// <summary>Test seam used by the in-memory ledger to move balances.</summary>
    public void Replace(Player player) => _players[player.Id] = player;

    public void Remove(PlayerId id) => _players.Remove(id);
}
