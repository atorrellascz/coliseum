using Coliseum.Domain.Battles;
using Coliseum.Domain.Players;
using StackExchange.Redis;

namespace Coliseum.Infrastructure.Redis.Keys;

/// <summary>
/// The whole key schema in one place (ARQ-05). Nothing else in the code base builds a key string.
/// <list type="table">
/// <item><term>{p}:player:{id}</term><description>hash: id, name, description, gold, silver, attack, defense, hitPoints, createdAt</description></item>
/// <item><term>{p}:player:name:{NORMALIZED}</term><description>string: player id (uniqueness guard, SET NX)</description></item>
/// <item><term>{p}:players:index</term><description>sorted set: createdAt (unix ms) -> id</description></item>
/// <item><term>{p}:battles:stream</term><description>stream: battleId, attackerId, defenderId, submittedAt</description></item>
/// <item><term>{p}:battles:dlq</term><description>stream: poison messages + reason</description></item>
/// <item><term>{p}:battle:{id}</term><description>hash: status, attackerId, defenderId, submittedAt, winnerId, loserId, gold, silver, score, report, processedAt, error</description></item>
/// <item><term>{p}:leaderboard</term><description>sorted set: score -> playerId</description></item>
/// <item><term>{p}:arena:events</term><description>pub/sub channel for live events</description></item>
/// </list>
/// </summary>
public sealed class RedisKeys(string prefix)
{
    public string Prefix { get; } = prefix;

    public RedisKey Player(PlayerId id) => $"{Prefix}:player:{id.Value}";

    public RedisKey PlayerName(string normalizedName) => $"{Prefix}:player:name:{normalizedName}";

    public RedisKey PlayersIndex => $"{Prefix}:players:index";

    public RedisKey BattlesStream => $"{Prefix}:battles:stream";

    public RedisKey BattlesDeadLetter => $"{Prefix}:battles:dlq";

    public RedisKey Battle(BattleId id) => $"{Prefix}:battle:{id.Value}";

    public RedisKey Leaderboard => $"{Prefix}:leaderboard";

    /// <summary>hash of back-office counters: processed, attackerWins, gold, silver, turns, turns:{bucket}</summary>
    public RedisKey Stats => $"{Prefix}:stats:battles";

    public RedisChannel EventsChannel => RedisChannel.Literal($"{Prefix}:arena:events");
}
