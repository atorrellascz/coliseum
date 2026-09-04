using System.Text.Json;
using Coliseum.Application.Ports;
using Coliseum.Contracts.Battles;
using Coliseum.Domain.Battles;
using Coliseum.Domain.Players;
using Coliseum.IntegrationTests.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace Coliseum.IntegrationTests.Redis;

/// <summary>apply_battle.lua line by line: atomic, idempotent, floors and caps, report round trip (ADR-03).</summary>
[Collection(RedisCollection.Name)]
public class BattleLedgerTests(RedisFixture redis)
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;
    private static readonly DateTimeOffset Now = new(2026, 9, 4, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Settlement_moves_resources_scores_the_winner_and_stores_the_report()
    {
        var world = await World.CreateAsync(redis, _ct, attackerGold: 500, attackerSilver: 120, defenderGold: 500, defenderSilver: 120);
        var report = world.Run("b1");

        var settlement = await world.Ledger.ApplyAsync(report, _ct);

        settlement.Outcome.ShouldBe(SettlementOutcome.Applied);
        var loser = (await world.Players.GetAsync(report.LoserId, _ct))!;
        var winner = (await world.Players.GetAsync(report.WinnerId, _ct))!;
        loser.Resources.ShouldBe(Resources.Unchecked(500 - settlement.GoldTransferred, 120 - settlement.SilverTransferred));
        winner.Resources.ShouldBe(Resources.Unchecked(500 + settlement.GoldTransferred, 120 + settlement.SilverTransferred));
        settlement.ShouldBe(new SettlementResult(SettlementOutcome.Applied, report.Loot.Gold, report.Loot.Silver));

        (await world.Leaderboard.GetEntryAsync(report.WinnerId, _ct))!.Score.ShouldBe(settlement.Score);
        (await world.Leaderboard.GetEntryAsync(report.LoserId, _ct)).ShouldBeNull();

        var record = (await world.Reports.GetAsync(report.BattleId, _ct))!;
        record.Status.ShouldBe(BattleStatus.Done);
        record.ProcessedAt.ShouldNotBeNull();
        JsonSerializer.Serialize(record.Report).ShouldBe(JsonSerializer.Serialize(report));
    }

    [Fact]
    public async Task Applying_twice_returns_the_original_amounts_and_changes_nothing()
    {
        var world = await World.CreateAsync(redis, _ct);
        var report = world.Run("b1");
        var first = await world.Ledger.ApplyAsync(report, _ct);
        var balancesAfterFirst = await world.BalancesAsync(_ct);

        var second = await world.Ledger.ApplyAsync(report, _ct);

        second.ShouldBe(first with { Outcome = SettlementOutcome.AlreadyApplied });
        (await world.BalancesAsync(_ct)).ShouldBe(balancesAfterFirst);
        (await world.Leaderboard.GetEntryAsync(report.WinnerId, _ct))!.Score.ShouldBe(first.Score);
    }

    [Fact]
    public async Task Loser_never_goes_negative_and_winner_is_capped_at_one_billion()
    {
        var world = await World.CreateAsync(redis, _ct, attackerGold: Resources.MaxPerResource - 1, attackerSilver: 0, defenderGold: Resources.MaxPerResource, defenderSilver: Resources.MaxPerResource, attackerAttack: 10_000, defenderAttack: 1, defenderDefense: 0);
        var report = world.Run("b1");
        report.WinnerId.ShouldBe(world.Attacker.Id); // 10,000 attack vs 100 hp: the attacker wins on turn 1

        var settlement = await world.Ledger.ApplyAsync(report, _ct);

        var winner = (await world.Players.GetAsync(report.WinnerId, _ct))!;
        var loser = (await world.Players.GetAsync(report.LoserId, _ct))!;
        settlement.GoldTransferred.ShouldBe(Resources.MaxPerResource * report.Loot.Percent / 100);
        winner.Resources.Gold.ShouldBe(Resources.MaxPerResource); // (1e9 - 1) + stolen exceeds the cap: excess burned (SUP-05)
        loser.Resources.Gold.ShouldBe(Resources.MaxPerResource - settlement.GoldTransferred);
        loser.Resources.Gold.ShouldBeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task Loot_is_computed_on_the_live_balance_at_settlement_time()
    {
        var world = await World.CreateAsync(redis, _ct, defenderGold: 1000, attackerGold: 1000, attackerAttack: 10_000, defenderDefense: 0);
        var report = world.Run("b1");

        // Between simulation and settlement the loser's gold changed (another battle, an admin fix): the script uses the current value.
        await redis.Multiplexer.GetDatabase().HashSetAsync(world.Keys.Player(report.LoserId), "gold", 100);
        var settlement = await world.Ledger.ApplyAsync(report, _ct);

        settlement.GoldTransferred.ShouldBe((100 * report.Loot.Percent + 99) / 100);
        settlement.GoldTransferred.ShouldNotBe(report.Loot.Gold);
    }

    [Fact]
    public async Task Missing_player_means_nothing_is_applied()
    {
        var world = await World.CreateAsync(redis, _ct);
        var report = world.Run("b1");
        await redis.Multiplexer.GetDatabase().KeyDeleteAsync(world.Keys.Player(report.LoserId));

        var settlement = await world.Ledger.ApplyAsync(report, _ct);

        settlement.Outcome.ShouldBe(SettlementOutcome.PlayerMissing);
        (await world.Reports.GetAsync(report.BattleId, _ct))!.Status.ShouldBe(BattleStatus.Queued);
    }

    /// <summary>Two seeded players plus the real adapters under a fresh prefix.</summary>
    private sealed class World
    {
        public required IPlayerRepository Players { get; init; }

        public required IBattleLedger Ledger { get; init; }

        public required ILeaderboard Leaderboard { get; init; }

        public required IBattleReportStore Reports { get; init; }

        public required Coliseum.Infrastructure.Redis.Keys.RedisKeys Keys { get; init; }

        public required Player Attacker { get; init; }

        public required Player Defender { get; init; }

        public static async Task<World> CreateAsync(
            RedisFixture redis,
            CancellationToken ct,
            long attackerGold = 500,
            long attackerSilver = 120,
            long defenderGold = 500,
            long defenderSilver = 120,
            int attackerAttack = 70,
            int defenderAttack = 70,
            int defenderDefense = 30)
        {
            string prefix = RedisFixture.NewPrefix();
            var services = redis.BuildServices(prefix);
            var players = services.GetRequiredService<IPlayerRepository>();
            var reports = services.GetRequiredService<IBattleReportStore>();

            var attacker = Player.Create(PlayerId.Unchecked("att"), "Att", "", attackerGold, attackerSilver, attackerAttack, 30, 100, Now).Value!;
            var defender = Player.Create(PlayerId.Unchecked("def"), "Def", "", defenderGold, defenderSilver, defenderAttack, defenderDefense, 100, Now).Value!;
            (await players.CreateAsync(attacker, ct)).ShouldBeTrue();
            (await players.CreateAsync(defender, ct)).ShouldBeTrue();
            await reports.CreateQueuedAsync(BattleId.Unchecked("b1"), attacker.Id, defender.Id, Now, ct);

            return new World
            {
                Players = players,
                Ledger = services.GetRequiredService<IBattleLedger>(),
                Leaderboard = services.GetRequiredService<ILeaderboard>(),
                Reports = reports,
                Keys = RedisFixture.KeysFor(prefix),
                Attacker = attacker,
                Defender = defender,
            };
        }

        public BattleReport Run(string battleId) => BattleEngine.Run(BattleId.Unchecked(battleId), Attacker, Defender).Value!;

        public async Task<(Resources Attacker, Resources Defender)> BalancesAsync(CancellationToken ct) =>
            ((await Players.GetAsync(Attacker.Id, ct))!.Resources, (await Players.GetAsync(Defender.Id, ct))!.Resources);
    }
}
