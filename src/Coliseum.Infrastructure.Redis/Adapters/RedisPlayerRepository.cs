using System.Globalization;
using Coliseum.Application.Ports;
using Coliseum.Domain.Players;
using Coliseum.Infrastructure.Redis.Keys;
using Coliseum.Infrastructure.Redis.Scripts;
using StackExchange.Redis;

namespace Coliseum.Infrastructure.Redis.Adapters;

/// <summary>
/// Players as Redis hashes. Creation goes through <c>create_player.lua</c> so the name guard and the hash are
/// written atomically; reads are HGETALL, batched in one pipeline for <see cref="GetManyAsync"/>.
/// </summary>
public sealed class RedisPlayerRepository(IConnectionMultiplexer redis, RedisKeys keys, LuaScripts scripts) : IPlayerRepository
{
    private const string FieldId = "id";
    private const string FieldName = "name";
    private const string FieldDescription = "description";
    private const string FieldGold = "gold";
    private const string FieldSilver = "silver";
    private const string FieldAttack = "attack";
    private const string FieldDefense = "defense";
    private const string FieldHitPoints = "hitPoints";
    private const string FieldCreatedAt = "createdAt";

    public async Task<bool> CreateAsync(Player player, CancellationToken cancellationToken)
    {
        var db = redis.GetDatabase();
        RedisKey[] scriptKeys = [keys.PlayerName(player.NormalizedName), keys.Player(player.Id), keys.PlayersIndex];
        RedisValue[] args =
        [
            player.Id.Value,
            player.CreatedAt.ToUnixTimeMilliseconds(),
            FieldId, player.Id.Value,
            FieldName, player.Name,
            FieldDescription, player.Description,
            FieldGold, player.Resources.Gold,
            FieldSilver, player.Resources.Silver,
            FieldAttack, player.Stats.Attack,
            FieldDefense, player.Stats.Defense,
            FieldHitPoints, player.Stats.HitPoints,
            FieldCreatedAt, player.CreatedAt.ToString("O", CultureInfo.InvariantCulture),
        ];

        var result = await db.ScriptEvaluateAsync(scripts.CreatePlayer, scriptKeys, args).WaitAsync(cancellationToken);
        return (long)result == 1;
    }

    public async Task<Player?> GetAsync(PlayerId id, CancellationToken cancellationToken)
    {
        var entries = await redis.GetDatabase().HashGetAllAsync(keys.Player(id)).WaitAsync(cancellationToken);
        return entries.Length == 0 ? null : Rehydrate(entries);
    }

    public async Task<IReadOnlyDictionary<PlayerId, Player>> GetManyAsync(IReadOnlyCollection<PlayerId> ids, CancellationToken cancellationToken)
    {
        var db = redis.GetDatabase();
        var batch = db.CreateBatch();
        var distinct = ids.Distinct().ToList();
        var reads = distinct.Select(id => batch.HashGetAllAsync(keys.Player(id))).ToList();
        batch.Execute();

        var found = new Dictionary<PlayerId, Player>(distinct.Count);
        for (int i = 0; i < distinct.Count; i++)
        {
            var entries = await reads[i].WaitAsync(cancellationToken);
            if (entries.Length > 0)
            {
                found[distinct[i]] = Rehydrate(entries);
            }
        }

        return found;
    }

    /// <summary>Storage is trusted: values were validated when written, so no re-validation (Player.Rehydrate).</summary>
    private static Player Rehydrate(HashEntry[] entries)
    {
        var map = entries.ToDictionary(e => (string)e.Name!, e => e.Value, StringComparer.Ordinal);
        return Player.Rehydrate(
            PlayerId.Unchecked((string)map[FieldId]!),
            (string)map[FieldName]!,
            (string?)map.GetValueOrDefault(FieldDescription) ?? string.Empty,
            Resources.Unchecked((long)map[FieldGold], (long)map[FieldSilver]),
            CombatStats.Unchecked((int)map[FieldAttack], (int)map[FieldDefense], (int)map[FieldHitPoints]),
            DateTimeOffset.Parse((string)map[FieldCreatedAt]!, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));
    }
}
