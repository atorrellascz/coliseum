using Coliseum.Application.Ports;
using Coliseum.Application.UseCases.Battles;
using Coliseum.Contracts.Battles;
using Coliseum.Contracts.Events;
using Coliseum.Domain.Battles;
using Coliseum.Domain.Players;
using Coliseum.UnitTests.Fakes;

namespace Coliseum.UnitTests.Application;

public class ProcessBattleHandlerTests
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task Processes_settles_and_publishes()
    {
        var world = new FakeWorld();
        var ata = world.Seed("ata", gold: 500, silver: 120);
        var bot = world.Seed("bot", gold: 500, silver: 120);
        var message = await Queue(world, ata.Id, bot.Id);
        world.Clock.Advance(TimeSpan.FromMilliseconds(250));

        var outcome = await world.ProcessBattle.HandleAsync(message, _ct);

        outcome.ShouldBe(ProcessOutcome.Processed);
        var record = world.Reports.All[message.BattleId];
        record.Status.ShouldBe(BattleStatus.Done);
        record.Report.ShouldNotBeNull();
        record.Settlement!.Outcome.ShouldBe(SettlementOutcome.Applied);

        var winner = world.Players.All[record.Report.WinnerId];
        var loser = world.Players.All[record.Report.LoserId];
        winner.Resources.Gold.ShouldBe(500 + record.Settlement.GoldTransferred);
        loser.Resources.Gold.ShouldBe(500 - record.Settlement.GoldTransferred);
        world.Leaderboard.ScoreOf(record.Report.WinnerId).ShouldBe(record.Settlement.Score);
        world.Leaderboard.ScoreOf(record.Report.LoserId).ShouldBe(0);

        var done = world.Events.Single<BattleDoneEvent>();
        done.Score.ShouldBe(record.Settlement.Score);
        done.WinnerId.ShouldBe(record.Report.WinnerId.Value);
    }

    [Fact]
    public async Task Redelivered_message_is_a_duplicate_and_changes_nothing()
    {
        var world = new FakeWorld();
        var ata = world.Seed("ata");
        var bot = world.Seed("bot");
        var message = await Queue(world, ata.Id, bot.Id);

        (await world.ProcessBattle.HandleAsync(message, _ct)).ShouldBe(ProcessOutcome.Processed);
        var goldAfterFirst = world.Players.All.Values.Select(p => p.Resources.Gold).ToList();

        var outcome = await world.ProcessBattle.HandleAsync(message with { DeliveryCount = 2 }, _ct);

        outcome.ShouldBe(ProcessOutcome.Duplicate);
        world.Ledger.Applications.ShouldBe(1);
        world.Players.All.Values.Select(p => p.Resources.Gold).ShouldBe(goldAfterFirst);
        world.Events.Events.OfType<BattleDoneEvent>().Count().ShouldBe(1);
    }

    [Fact]
    public async Task Missing_player_marks_the_battle_failed()
    {
        var world = new FakeWorld();
        var ata = world.Seed("ata");
        var bot = world.Seed("bot");
        var message = await Queue(world, ata.Id, bot.Id);
        world.Players.Remove(bot.Id);

        var outcome = await world.ProcessBattle.HandleAsync(message, _ct);

        outcome.ShouldBe(ProcessOutcome.PlayerMissing);
        var record = world.Reports.All[message.BattleId];
        record.Status.ShouldBe(BattleStatus.Failed);
        record.Error.ShouldBe("player_missing");
        world.Events.Single<BattleFailedEvent>().Error.ShouldBe("player_missing");
        world.Ledger.Applications.ShouldBe(0);
    }

    [Fact]
    public async Task Engine_invariant_failure_marks_the_battle_failed()
    {
        var world = new FakeWorld { Rules = new BattleRules(MaxTurns: 1) };
        var ata = world.Seed("ata");
        var bot = world.Seed("bot");
        var message = await Queue(world, ata.Id, bot.Id);

        var outcome = await world.ProcessBattle.HandleAsync(message, _ct);

        outcome.ShouldBe(ProcessOutcome.Failed);
        world.Reports.All[message.BattleId].Error.ShouldBe("battle.max_turns");
    }

    private async Task<QueuedBattle> Queue(FakeWorld world, PlayerId attacker, PlayerId defender)
    {
        var id = BattleId.Unchecked(world.Ids.NewId());
        await world.Reports.CreateQueuedAsync(id, attacker, defender, world.Clock.UtcNow, _ct);
        await world.Queue.EnqueueAsync(id, attacker, defender, world.Clock.UtcNow, _ct);
        return (await world.Queue.ReadAsync("worker-1", 1, TimeSpan.Zero, _ct)).Single();
    }
}
