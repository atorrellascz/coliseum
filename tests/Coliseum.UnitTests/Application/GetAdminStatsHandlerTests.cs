using Coliseum.Application;
using Coliseum.Contracts.Battles;
using Coliseum.UnitTests.Fakes;

namespace Coliseum.UnitTests.Application;

public class GetAdminStatsHandlerTests
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task Empty_store_gives_zeros_and_empty_top()
    {
        var world = new FakeWorld();

        var stats = await world.GetAdminStats.HandleAsync(_ct);

        stats.GeneratedAt.ShouldBe(world.Clock.UtcNow);
        stats.Economy.BattlesProcessed.ShouldBe(0);
        stats.Economy.AttackerWinRate.ShouldBe(0);
        stats.Economy.TurnBuckets.Keys.ShouldBe(["1-5", "6-10", "11-20", "21-50", "51+"], ignoreOrder: true);
        stats.Queue.ShouldBe(new(0, 0, 0));
        stats.Top.ShouldBeEmpty();
    }

    [Fact]
    public async Task Processed_battles_feed_the_economy_and_the_leaderboard()
    {
        var world = new FakeWorld();
        var ata = world.Seed("ata");
        var bot = world.Seed("bot");
        for (int i = 0; i < 3; i++)
        {
            var submitted = await world.SubmitBattle.HandleAsync(Caller.Service, new SubmitBattleRequest(bot.Id.Value, ata.Id.Value), _ct);
            submitted.IsSuccess.ShouldBeTrue();
            var message = (await world.Queue.ReadAsync("w", 1, TimeSpan.Zero, _ct)).Single();
            await world.ProcessBattle.HandleAsync(message, _ct);
            await world.Queue.AcknowledgeAsync(message.MessageId, _ct);
        }

        var stats = await world.GetAdminStats.HandleAsync(_ct);

        stats.Economy.BattlesProcessed.ShouldBe(3);
        stats.Economy.AttackerWins.ShouldBeInRange(0, 3);
        stats.Economy.AttackerWinRate.ShouldBe(Math.Round(stats.Economy.AttackerWins / 3.0, 4));
        (stats.Economy.GoldStolen + stats.Economy.SilverStolen).ShouldBe(stats.Top.Sum(e => e.Score));
        stats.Economy.TurnBuckets.Values.Sum().ShouldBe(3);
        stats.Economy.AverageTurns.ShouldBeGreaterThan(0);
        stats.Queue.Pending.ShouldBe(0);
        stats.Queue.Length.ShouldBe(3);
    }
}
