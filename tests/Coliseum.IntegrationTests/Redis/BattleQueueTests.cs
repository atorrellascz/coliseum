using Coliseum.Application.Ports;
using Coliseum.Domain.Battles;
using Coliseum.Domain.Players;
using Coliseum.IntegrationTests.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace Coliseum.IntegrationTests.Redis;

/// <summary>The delivery guarantees behind "none skipped, none twice" (REQ-06), against a real stream.</summary>
[Collection(RedisCollection.Name)]
public class BattleQueueTests(RedisFixture redis)
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;
    private static readonly DateTimeOffset Now = new(2026, 9, 4, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Entries_are_delivered_in_submission_order_exactly_once_per_read()
    {
        var queue = await NewQueueAsync();
        for (int i = 0; i < 100; i++)
        {
            await queue.EnqueueAsync(BattleId.Unchecked($"b{i:D3}"), PlayerId.Unchecked("a"), PlayerId.Unchecked("d"), Now, _ct);
        }

        var delivered = new List<QueuedBattle>();
        while (delivered.Count < 100)
        {
            var batch = await queue.ReadAsync("w1", 32, TimeSpan.Zero, _ct);
            batch.ShouldNotBeEmpty();
            delivered.AddRange(batch);
        }

        delivered.Select(b => b.BattleId.Value).ShouldBe(Enumerable.Range(0, 100).Select(i => $"b{i:D3}"));
        delivered.ShouldAllBe(b => b.DeliveryCount == 1);
        (await queue.ReadAsync("w1", 32, TimeSpan.Zero, _ct)).ShouldBeEmpty();
        (await queue.GetStatsAsync(_ct)).Pending.ShouldBe(100);
    }

    [Fact]
    public async Task Unacknowledged_entry_of_a_dead_consumer_is_reclaimed_with_a_higher_delivery_count()
    {
        var queue = await NewQueueAsync();
        await queue.EnqueueAsync(BattleId.Unchecked("b1"), PlayerId.Unchecked("a"), PlayerId.Unchecked("d"), Now, _ct);

        var first = (await queue.ReadAsync("crashed-worker", 1, TimeSpan.Zero, _ct)).Single();
        // No XACK: simulate a crash. Another consumer takes over after the idle threshold.
        await Task.Delay(60, _ct);
        var claimed = await queue.ClaimStaleAsync("survivor", TimeSpan.FromMilliseconds(20), 10, _ct);

        claimed.Single().MessageId.ShouldBe(first.MessageId);
        claimed.Single().DeliveryCount.ShouldBe(2);

        await queue.AcknowledgeAsync(first.MessageId, _ct);
        (await queue.GetStatsAsync(_ct)).Pending.ShouldBe(0);
        (await queue.ClaimStaleAsync("survivor", TimeSpan.Zero, 10, _ct)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Dead_lettering_acknowledges_the_entry_and_records_it_in_the_dlq()
    {
        var queue = await NewQueueAsync();
        await queue.EnqueueAsync(BattleId.Unchecked("poison"), PlayerId.Unchecked("a"), PlayerId.Unchecked("d"), Now, _ct);
        var message = (await queue.ReadAsync("w1", 1, TimeSpan.Zero, _ct)).Single();

        await queue.DeadLetterAsync(message with { DeliveryCount = 5 }, "boom", _ct);

        var stats = await queue.GetStatsAsync(_ct);
        stats.Pending.ShouldBe(0);
        stats.DeadLettered.ShouldBe(1);
        stats.Length.ShouldBe(1);
    }

    [Fact]
    public async Task Initialize_is_idempotent_and_reading_before_any_entry_returns_nothing()
    {
        var queue = await NewQueueAsync();
        await queue.InitializeAsync(_ct);

        (await queue.ReadAsync("w1", 10, TimeSpan.FromMilliseconds(10), _ct)).ShouldBeEmpty();
        (await queue.GetStatsAsync(_ct)).ShouldBe(new QueueStats(0, 0, 0));
    }

    private async Task<IBattleQueue> NewQueueAsync()
    {
        var services = redis.BuildServices(RedisFixture.NewPrefix());
        var queue = services.GetRequiredService<IBattleQueue>();
        await queue.InitializeAsync(_ct);
        return queue;
    }
}
