using Coliseum.Application;
using Coliseum.Contracts.Battles;
using Coliseum.Domain.Common;
using Coliseum.UnitTests.Fakes;

namespace Coliseum.UnitTests.Application;

public class GetBattleHandlerTests
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task Participant_reads_the_full_report_with_narrative()
    {
        var world = new FakeWorld();
        var ata = world.Seed("ata");
        var bot = world.Seed("bot");
        string battleId = await PlayAsync(world, ata.Id.Value, bot.Id.Value);

        var result = await world.GetBattle.HandleAsync(Caller.ForPlayer(bot.Id), battleId, _ct);

        result.IsSuccess.ShouldBeTrue();
        var response = result.Value;
        response.Status.ShouldBe(BattleStatus.Done);
        response.Events.ShouldNotBeNull().Count.ShouldBe(response.Turns!.Value);
        response.Loot.ShouldNotBeNull().Score.ShouldBe(response.Loot.Gold + response.Loot.Silver);
        response.Narrative.ShouldNotBeNull();
        response.Narrative.Count.ShouldBe(response.Turns.Value + 2);
        response.Narrative[0].ShouldBe($"ata challenges bot. Seed {response.Seed}.");
        response.Narrative[^1].ShouldStartWith(response.WinnerId == "ata" ? "ata wins after" : "bot wins after");
    }

    [Fact]
    public async Task Queued_battle_shows_only_header_fields()
    {
        var world = new FakeWorld();
        var ata = world.Seed("ata");
        world.Seed("bot");
        var submitted = await world.SubmitBattle.HandleAsync(Caller.ForPlayer(ata.Id), new SubmitBattleRequest("bot"), _ct);

        var result = await world.GetBattle.HandleAsync(Caller.ForPlayer(ata.Id), submitted.Value!.BattleId, _ct);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Status.ShouldBe(BattleStatus.Queued);
        result.Value.Report().ShouldBeNull();
    }

    [Fact]
    public async Task Non_participant_gets_not_found_not_forbidden()
    {
        var world = new FakeWorld();
        var ata = world.Seed("ata");
        var bot = world.Seed("bot");
        var stranger = world.Seed("stranger");
        string battleId = await PlayAsync(world, ata.Id.Value, bot.Id.Value);

        var result = await world.GetBattle.HandleAsync(Caller.ForPlayer(stranger.Id), battleId, _ct);

        result.ErrorKind.ShouldBe(DomainErrorKind.NotFound);
    }

    [Fact]
    public async Task Service_token_reads_any_battle_and_unknown_ids_are_not_found()
    {
        var world = new FakeWorld();
        var ata = world.Seed("ata");
        var bot = world.Seed("bot");
        string battleId = await PlayAsync(world, ata.Id.Value, bot.Id.Value);

        (await world.GetBattle.HandleAsync(Caller.Service, battleId, _ct)).IsSuccess.ShouldBeTrue();
        (await world.GetBattle.HandleAsync(Caller.Service, "missing", _ct)).ErrorKind.ShouldBe(DomainErrorKind.NotFound);
        (await world.GetBattle.HandleAsync(Caller.Service, "bad id!", _ct)).ErrorKind.ShouldBe(DomainErrorKind.NotFound);
    }

    private async Task<string> PlayAsync(FakeWorld world, string attacker, string defender)
    {
        var submitted = await world.SubmitBattle.HandleAsync(Caller.Service, new SubmitBattleRequest(defender, attacker), _ct);
        var message = (await world.Queue.ReadAsync("w", 1, TimeSpan.Zero, _ct)).Single();
        await world.ProcessBattle.HandleAsync(message, _ct);
        return submitted.Value!.BattleId;
    }
}

file static class Extensions
{
    /// <summary>All report-only fields as one nullable, so "no report yet" is a single assertion.</summary>
    public static object? Report(this BattleReportResponse response) =>
        response.WinnerId ?? (object?)response.Turns ?? response.Loot ?? response.Events ?? (object?)response.Narrative;
}
