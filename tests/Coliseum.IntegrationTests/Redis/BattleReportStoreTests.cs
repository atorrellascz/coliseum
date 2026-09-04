using Coliseum.Application.Ports;
using Coliseum.Contracts.Battles;
using Coliseum.Domain.Battles;
using Coliseum.Domain.Players;
using Coliseum.IntegrationTests.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace Coliseum.IntegrationTests.Redis;

[Collection(RedisCollection.Name)]
public class BattleReportStoreTests(RedisFixture redis)
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;
    private static readonly DateTimeOffset Now = new(2026, 9, 4, 12, 0, 0, TimeSpan.Zero);
    private static readonly BattleId Id = BattleId.Unchecked("b1");

    [Fact]
    public async Task Lifecycle_queued_processing_failed_and_done_is_never_overwritten()
    {
        string prefix = RedisFixture.NewPrefix();
        using var services = redis.BuildServices(prefix);
        var store = services.GetRequiredService<IBattleReportStore>();

        (await store.GetAsync(Id, _ct)).ShouldBeNull();

        await store.CreateQueuedAsync(Id, PlayerId.Unchecked("a"), PlayerId.Unchecked("d"), Now, _ct);
        var queued = (await store.GetAsync(Id, _ct))!;
        queued.Status.ShouldBe(BattleStatus.Queued);
        queued.SubmittedAt.ShouldBe(Now);
        queued.Report.ShouldBeNull();

        await store.MarkProcessingAsync(Id, _ct);
        (await store.GetAsync(Id, _ct))!.Status.ShouldBe(BattleStatus.Processing);

        await store.MarkFailedAsync(Id, "player_missing", Now.AddSeconds(1), _ct);
        var failed = (await store.GetAsync(Id, _ct))!;
        failed.Status.ShouldBe(BattleStatus.Failed);
        failed.Error.ShouldBe("player_missing");
        failed.ProcessedAt.ShouldBe(Now.AddSeconds(1));

        // Simulate the ledger having settled the battle; later marks must not regress it.
        await redis.Multiplexer.GetDatabase().HashSetAsync(RedisFixture.KeysFor(prefix).Battle(Id), "status", "done");
        await store.MarkProcessingAsync(Id, _ct);
        await store.MarkFailedAsync(Id, "late", Now, _ct);
        (await redis.Multiplexer.GetDatabase().HashGetAsync(RedisFixture.KeysFor(prefix).Battle(Id), "status")).ToString().ShouldBe("done");
    }
}
