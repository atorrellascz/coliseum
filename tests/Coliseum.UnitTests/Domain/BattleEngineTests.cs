using System.Text.Json;
using Coliseum.Domain.Battles;
using Coliseum.Domain.Common;
using Coliseum.Domain.Players;
using Coliseum.UnitTests.Fakes;

namespace Coliseum.UnitTests.Domain;

/// <summary>Rule-by-rule verification of the engine, starting with the literal examples of the spec.</summary>
public class BattleEngineTests
{
    private static readonly BattleId Battle = BattleId.Unchecked("battle-1");

    // ---- Spec examples ------------------------------------------------------------------------------

    [Theory]
    [InlineData(70, 100, 70)] // full health: full attack
    [InlineData(70, 90, 63)]  // spec: losing 10% health reduces attack by 10%
    [InlineData(70, 50, 35)]  // exactly at the floor
    [InlineData(70, 10, 35)]  // spec: never below 35 (50% of 70)
    [InlineData(70, 1, 35)]
    [InlineData(7, 1, 4)]     // odd base: floor is ceil(3.5) = 4
    [InlineData(1, 1, 1)]     // attack never reaches 0, so battles end
    public void Current_attack_decays_with_health_down_to_half_of_base(int baseAttack, int hp, int expected)
    {
        BattleEngine.CurrentAttack(baseAttack, hp, 100, 50).ShouldBe(expected);
    }

    [Fact]
    public void Loot_matches_the_spec_example()
    {
        var loot = LootResult.Compute(Resources.Unchecked(500, 120), 7);

        loot.ShouldBe(new LootResult(7, 35, 9));
        loot.Total.ShouldBe(44);
    }

    // ---- Turn order ---------------------------------------------------------------------------------

    [Fact]
    public void Initiator_attacks_first_and_roles_alternate()
    {
        var attacker = TestPlayers.Create("att");
        var defender = TestPlayers.Create("def");

        var report = Run(attacker, defender);

        report.Events[0].AttackerId.ShouldBe(attacker.Id);
        for (int i = 0; i < report.Events.Count; i++)
        {
            var expectedAttacker = i % 2 == 0 ? attacker.Id : defender.Id;
            report.Events[i].AttackerId.ShouldBe(expectedAttacker);
            report.Events[i].Turn.ShouldBe(i + 1);
        }
    }

    // ---- Hit or miss --------------------------------------------------------------------------------

    [Fact]
    public void With_zero_defense_every_attack_lands_and_damage_equals_attack()
    {
        var report = Run(TestPlayers.Create("att", defense: 0), TestPlayers.Create("def", defense: 0));

        report.Events.ShouldAllBe(e => e.Hit && e.DodgeChanceBasisPoints == 0 && e.Damage == e.AttackValueUsed);
    }

    [Fact]
    public void Hit_is_decided_by_roll_against_dodge_chance()
    {
        // Equal attack and defense (70 vs 70) -> dodge 5000 bp. Roll 4999 misses, roll 5000 hits.
        // Then two guaranteed hits (9999) end the fight in 4 turns; the last value is the loot percent.
        var attacker = TestPlayers.Create("att", attack: 70, defense: 70, hitPoints: 100);
        var defender = TestPlayers.Create("def", attack: 70, defense: 70, hitPoints: 100);
        var scripted = new SequenceRandom(4999, 5000, 9999, 9999, 7);

        var report = Run(attacker, defender, random: scripted);

        report.Events[0].ShouldSatisfyAllConditions(
            e => e.DodgeChanceBasisPoints.ShouldBe(5000),
            e => e.Roll.ShouldBe(4999),
            e => e.Hit.ShouldBeFalse(),
            e => e.Damage.ShouldBe(0),
            e => e.DefenderHpAfter.ShouldBe(100));
        report.Events[1].ShouldSatisfyAllConditions(
            e => e.Roll.ShouldBe(5000),
            e => e.Hit.ShouldBeTrue(),
            e => e.Damage.ShouldBe(70),
            e => e.DefenderHpAfter.ShouldBe(30));
        report.Loot.Percent.ShouldBe(7);
    }

    [Theory]
    [InlineData(0, 70, 0)]        // no defense: never dodges
    [InlineData(70, 70, 5000)]    // even match: 50%
    [InlineData(30, 70, 3000)]    // 30 / 100
    [InlineData(10_000, 1, 7500)] // capped at 75% so attacks can always land
    public void Dodge_chance_is_defense_over_defense_plus_attack_capped(int defense, int attack, int expected)
    {
        BattleEngine.DodgeBasisPoints(defense, attack, 7500).ShouldBe(expected);
    }

    [Fact]
    public void Dodge_chance_grows_with_defense_and_never_exceeds_the_cap()
    {
        int previous = -1;
        for (int defense = 0; defense <= 10_000; defense += 250)
        {
            int dodge = BattleEngine.DodgeBasisPoints(defense, 100, 7500);
            dodge.ShouldBeGreaterThanOrEqualTo(previous);
            dodge.ShouldBeLessThanOrEqualTo(7500);
            previous = dodge;
        }
    }

    // ---- Victory ------------------------------------------------------------------------------------

    [Fact]
    public void Battle_ends_exactly_when_a_player_reaches_zero_hit_points()
    {
        var report = Run(TestPlayers.Create("att"), TestPlayers.Create("def"));

        report.Events[^1].DefenderHpAfter.ShouldBe(0);
        report.Events[^1].DefenderId.ShouldBe(report.LoserId);
        report.Events.Take(report.Events.Count - 1).ShouldAllBe(e => e.DefenderHpAfter > 0);
        report.Turns.ShouldBe(report.Events.Count);
        report.WinnerId.ShouldNotBe(report.LoserId);
        (report.AttackerHpRemaining == 0 || report.DefenderHpRemaining == 0).ShouldBeTrue();
        (report.AttackerHpRemaining > 0 || report.DefenderHpRemaining > 0).ShouldBeTrue();
    }

    [Fact]
    public void Battle_with_extreme_defense_still_terminates()
    {
        var report = Run(
            TestPlayers.Create("att", attack: 1, defense: 10_000, hitPoints: 50),
            TestPlayers.Create("def", attack: 1, defense: 10_000, hitPoints: 50));

        report.Turns.ShouldBeGreaterThan(0);
        report.Events.ShouldAllBe(e => e.DodgeChanceBasisPoints == 7500);
    }

    [Fact]
    public void Exceeding_the_turn_guard_is_an_invariant_failure()
    {
        var result = BattleEngine.Run(Battle, TestPlayers.Create("att"), TestPlayers.Create("def"), new BattleRules(MaxTurns: 1));

        result.IsFailure.ShouldBeTrue();
        result.Errors.Single().ShouldSatisfyAllConditions(
            e => e.Kind.ShouldBe(DomainErrorKind.Invariant),
            e => e.Code.ShouldBe("battle.max_turns"));
    }

    [Fact]
    public void A_player_cannot_battle_themselves()
    {
        var player = TestPlayers.Create("same");

        var result = BattleEngine.Run(Battle, player, player);

        result.IsFailure.ShouldBeTrue();
        result.Errors.Single().Code.ShouldBe("battle.self");
    }

    // ---- Loot ---------------------------------------------------------------------------------------

    [Fact]
    public void Loot_percent_stays_within_5_and_10_and_never_exceeds_the_loser_balance()
    {
        for (int i = 0; i < 100; i++)
        {
            var attacker = TestPlayers.Create("att", gold: 3, silver: 1_000_000_000);
            var defender = TestPlayers.Create("def", gold: 3, silver: 1_000_000_000);

            var report = Run(attacker, defender, BattleId.Unchecked($"loot-{i}"));

            report.Loot.Percent.ShouldBeInRange(5, 10);
            report.Loot.Gold.ShouldBeInRange(1, 3);
            report.Loot.Silver.ShouldBeInRange(50_000_000, 100_000_000);
        }
    }

    // ---- Determinism --------------------------------------------------------------------------------

    [Fact]
    public void Same_battle_id_produces_the_same_report()
    {
        var first = Run(TestPlayers.Create("att"), TestPlayers.Create("def"));
        var second = Run(TestPlayers.Create("att"), TestPlayers.Create("def"));

        JsonSerializer.Serialize(second).ShouldBe(JsonSerializer.Serialize(first));
        first.Seed.ShouldBe(second.Seed);
    }

    [Fact]
    public void Different_battle_ids_produce_different_rolls()
    {
        var a = Run(TestPlayers.Create("att"), TestPlayers.Create("def"), BattleId.Unchecked("battle-a"));
        var b = Run(TestPlayers.Create("att"), TestPlayers.Create("def"), BattleId.Unchecked("battle-b"));

        a.Seed.ShouldNotBe(b.Seed);
        a.Events.Take(5).Select(e => e.Roll).ShouldNotBe(b.Events.Take(5).Select(e => e.Roll));
    }

    [Fact]
    public void Default_rules_are_valid_and_bad_rules_are_reported()
    {
        BattleRules.Default.Validate().ShouldBeEmpty();
        new BattleRules(MinAttackPercent: 0, MinLootPercent: 11, MaxLootPercent: 10, MaxDodgeBasisPoints: 10_000, MaxTurns: 0)
            .Validate().Select(e => e.Code)
            .ShouldBe(["rules.min_attack_percent", "rules.loot_percent", "rules.max_dodge", "rules.max_turns"]);
    }

    private static BattleReport Run(Player attacker, Player defender, BattleId? id = null, SequenceRandom? random = null)
    {
        var result = BattleEngine.Run(id ?? Battle, attacker, defender, random: random);
        result.IsSuccess.ShouldBeTrue();
        return result.Value;
    }
}
