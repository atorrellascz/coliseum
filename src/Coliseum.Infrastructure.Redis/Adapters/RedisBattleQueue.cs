using System.Globalization;
using Coliseum.Application.Ports;
using Coliseum.Domain.Battles;
using Coliseum.Domain.Players;
using Coliseum.Infrastructure.Redis.Connection;
using Coliseum.Infrastructure.Redis.Keys;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Coliseum.Infrastructure.Redis.Adapters;

/// <summary>
/// Battle queue on a Redis Stream with a consumer group (ADR-02).
/// XADD gives monotonic ids (submission order); XREADGROUP delivers each entry to one consumer and keeps it in
/// the Pending Entries List until XACK; XAUTOCLAIM takes over entries of consumers that died.
/// Reads are non-blocking: StackExchange.Redis multiplexes one connection and does not support BLOCK, so the
/// worker polls with a short sleep when the stream is empty (SUP-15, latency of a few hundred ms at most).
/// </summary>
public sealed class RedisBattleQueue(IConnectionMultiplexer redis, RedisKeys keys, IOptions<RedisOptions> options) : IBattleQueue
{
    private const string FieldBattleId = "battleId";
    private const string FieldAttackerId = "attackerId";
    private const string FieldDefenderId = "defenderId";
    private const string FieldSubmittedAt = "submittedAt";
    private const string FieldReason = "reason";
    private const string FieldDeliveryCount = "deliveryCount";

    private readonly string _group = options.Value.ConsumerGroup;
    private readonly long _maxLength = options.Value.StreamMaxLength;

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        var db = redis.GetDatabase();
        try
        {
            // "$" would skip entries added before the group existed; "0-0" makes the first worker see everything.
            await db.StreamCreateConsumerGroupAsync(keys.BattlesStream, _group, StreamPosition.Beginning, createStream: true).WaitAsync(cancellationToken);
        }
        catch (RedisServerException ex) when (ex.Message.StartsWith("BUSYGROUP", StringComparison.Ordinal))
        {
            // Group already exists: expected on every restart.
        }
    }

    public async Task EnqueueAsync(BattleId battleId, PlayerId attackerId, PlayerId defenderId, DateTimeOffset submittedAt, CancellationToken cancellationToken)
    {
        NameValueEntry[] fields =
        [
            new(FieldBattleId, battleId.Value),
            new(FieldAttackerId, attackerId.Value),
            new(FieldDefenderId, defenderId.Value),
            new(FieldSubmittedAt, submittedAt.ToString("O", CultureInfo.InvariantCulture)),
        ];

        await redis.GetDatabase()
            .StreamAddAsync(keys.BattlesStream, fields, maxLength: _maxLength, useApproximateMaxLength: true)
            .WaitAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<QueuedBattle>> ReadAsync(string consumer, int count, TimeSpan block, CancellationToken cancellationToken)
    {
        var entries = await redis.GetDatabase()
            .StreamReadGroupAsync(keys.BattlesStream, _group, consumer, StreamPosition.NewMessages, count)
            .WaitAsync(cancellationToken);

        if (entries.Length == 0 && block > TimeSpan.Zero)
        {
            await Task.Delay(block, cancellationToken);
            return [];
        }

        return entries.Select(e => ToBattle(e, deliveryCount: 1)).ToList();
    }

    public async Task<IReadOnlyList<QueuedBattle>> ClaimStaleAsync(string consumer, TimeSpan minIdle, int count, CancellationToken cancellationToken)
    {
        var db = redis.GetDatabase();
        var claimed = await db
            .StreamAutoClaimAsync(keys.BattlesStream, _group, consumer, (long)minIdle.TotalMilliseconds, StreamPosition.Beginning, count)
            .WaitAsync(cancellationToken);

        if (claimed.ClaimedEntries.Length == 0)
        {
            return [];
        }

        // XAUTOCLAIM does not report delivery counts; XPENDING for this consumer does.
        var pending = await db.StreamPendingMessagesAsync(keys.BattlesStream, _group, count, consumer).WaitAsync(cancellationToken);
        var deliveries = pending.ToDictionary(p => (string)p.MessageId!, p => p.DeliveryCount, StringComparer.Ordinal);

        return claimed.ClaimedEntries
            .Select(e => ToBattle(e, deliveries.TryGetValue((string)e.Id!, out int n) ? n : 2))
            .ToList();
    }

    public Task AcknowledgeAsync(string messageId, CancellationToken cancellationToken) =>
        redis.GetDatabase().StreamAcknowledgeAsync(keys.BattlesStream, _group, messageId).WaitAsync(cancellationToken);

    public async Task DeadLetterAsync(QueuedBattle battle, string reason, CancellationToken cancellationToken)
    {
        var db = redis.GetDatabase();
        NameValueEntry[] fields =
        [
            new(FieldBattleId, battle.BattleId.Value),
            new(FieldAttackerId, battle.AttackerId.Value),
            new(FieldDefenderId, battle.DefenderId.Value),
            new(FieldSubmittedAt, battle.SubmittedAt.ToString("O", CultureInfo.InvariantCulture)),
            new(FieldDeliveryCount, battle.DeliveryCount),
            new(FieldReason, reason),
        ];

        await db.StreamAddAsync(keys.BattlesDeadLetter, fields).WaitAsync(cancellationToken);
        await db.StreamAcknowledgeAsync(keys.BattlesStream, _group, battle.MessageId).WaitAsync(cancellationToken);
    }

    public async Task<QueueStats> GetStatsAsync(CancellationToken cancellationToken)
    {
        var db = redis.GetDatabase();
        long length = await db.StreamLengthAsync(keys.BattlesStream).WaitAsync(cancellationToken);
        long deadLettered = await db.StreamLengthAsync(keys.BattlesDeadLetter).WaitAsync(cancellationToken);

        long pending = 0;
        try
        {
            var info = await db.StreamPendingAsync(keys.BattlesStream, _group).WaitAsync(cancellationToken);
            pending = info.PendingMessageCount;
        }
        catch (RedisServerException ex) when (ex.Message.StartsWith("NOGROUP", StringComparison.Ordinal))
        {
            // No worker has initialized the group yet.
        }

        return new QueueStats(length, pending, deadLettered);
    }

    private static QueuedBattle ToBattle(StreamEntry entry, int deliveryCount)
    {
        var map = entry.Values.ToDictionary(v => (string)v.Name!, v => (string)v.Value!, StringComparer.Ordinal);
        return new QueuedBattle(
            (string)entry.Id!,
            BattleId.Unchecked(map[FieldBattleId]),
            PlayerId.Unchecked(map[FieldAttackerId]),
            PlayerId.Unchecked(map[FieldDefenderId]),
            DateTimeOffset.Parse(map[FieldSubmittedAt], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            deliveryCount);
    }
}
