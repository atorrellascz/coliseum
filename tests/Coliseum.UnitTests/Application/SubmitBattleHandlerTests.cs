using Coliseum.Application;
using Coliseum.Contracts.Battles;
using Coliseum.Contracts.Events;
using Coliseum.Domain.Common;
using Coliseum.UnitTests.Fakes;

namespace Coliseum.UnitTests.Application;

public class SubmitBattleHandlerTests
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task Player_token_attacks_as_itself_and_gets_202_with_record_written_before_the_message()
    {
        var world = new FakeWorld();
        var ata = world.Seed("ata");
        world.Seed("bot");

        var result = await world.SubmitBattle.HandleAsync(Caller.ForPlayer(ata.Id), new SubmitBattleRequest("bot"), _ct);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Status.ShouldBe(BattleStatus.Queued);
        result.Value.BattleId.ShouldBe("id-0001");

        var record = world.Reports.All.Values.Single();
        record.AttackerId.ShouldBe(ata.Id);
        record.Status.ShouldBe(BattleStatus.Queued);
        (await world.Queue.GetStatsAsync(_ct)).Length.ShouldBe(1);
        world.Events.Single<BattleQueuedEvent>().BattleId.ShouldBe("id-0001");
        world.Log.ShouldBe(["reports:create", "queue:enqueue", "events:publish"]);
    }

    [Fact]
    public async Task Player_cannot_attack_on_behalf_of_another_player()
    {
        var world = new FakeWorld();
        var ata = world.Seed("ata");
        world.Seed("bot");
        world.Seed("victim");

        var result = await world.SubmitBattle.HandleAsync(Caller.ForPlayer(ata.Id), new SubmitBattleRequest("bot", AttackerId: "victim"), _ct);

        result.ErrorKind.ShouldBe(DomainErrorKind.Forbidden);
        result.Errors.Single().Code.ShouldBe("battle.attacker.mismatch");
        world.Reports.All.ShouldBeEmpty();
    }

    [Fact]
    public async Task Service_token_must_name_the_attacker()
    {
        var world = new FakeWorld();
        world.Seed("bot");

        var result = await world.SubmitBattle.HandleAsync(Caller.Service, new SubmitBattleRequest("bot"), _ct);

        result.Errors.Single().Code.ShouldBe("battle.attacker.required");
    }

    [Fact]
    public async Task Service_token_can_submit_for_any_attacker()
    {
        var world = new FakeWorld();
        world.Seed("ata");
        world.Seed("bot");

        var result = await world.SubmitBattle.HandleAsync(Caller.Service, new SubmitBattleRequest("bot", AttackerId: "ata"), _ct);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task Self_battle_is_rejected_before_touching_storage()
    {
        var world = new FakeWorld();
        var ata = world.Seed("ata");

        var result = await world.SubmitBattle.HandleAsync(Caller.ForPlayer(ata.Id), new SubmitBattleRequest("ata"), _ct);

        result.Errors.Single().Code.ShouldBe("battle.self");
        world.Log.ShouldBeEmpty();
    }

    [Fact]
    public async Task Unknown_defender_is_not_found()
    {
        var world = new FakeWorld();
        var ata = world.Seed("ata");

        var result = await world.SubmitBattle.HandleAsync(Caller.ForPlayer(ata.Id), new SubmitBattleRequest("ghost"), _ct);

        result.ErrorKind.ShouldBe(DomainErrorKind.NotFound);
        world.Reports.All.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("bad id!")]
    public async Task Malformed_defender_id_is_a_validation_error(string? defenderId)
    {
        var world = new FakeWorld();
        var ata = world.Seed("ata");

        var result = await world.SubmitBattle.HandleAsync(Caller.ForPlayer(ata.Id), new SubmitBattleRequest(defenderId), _ct);

        result.ErrorKind.ShouldBe(DomainErrorKind.Validation);
        result.Errors.Single().Field.ShouldBe("defenderId");
    }
}
