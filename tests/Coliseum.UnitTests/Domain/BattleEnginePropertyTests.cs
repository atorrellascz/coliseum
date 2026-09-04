using Coliseum.Domain.Battles;
using Coliseum.Domain.Players;
using Coliseum.Domain.Randomness;

namespace Coliseum.UnitTests.Domain;

/// <summary>
/// Property-style test: thousands of random battles, every one checked against the invariants the rules imply.
/// Stats are generated with the engine's own PRNG so the test is itself deterministic and reproducible.
/// </summary>
public class BattleEnginePropertyTests
{
    private const int Battles = 2_000;

    [Fact]
    public void Every_random_battle_honours_all_engine_invariants()
    {
        var rng = new Xoshiro256StarStar(20260903);

        for (int i = 0; i < Battles; i++)
        {
            var attacker = RandomPlayer(rng, "att");
            var defender = RandomPlayer(rng, "def");
            var battleId = BattleId.Unchecked($"prop-{i}");

            var result = BattleEngine.Run(battleId, attacker, defender);

            result.IsSuccess.ShouldBeTrue($"battle {i} failed: {string.Join(", ", result.Errors.Select(e => e.Code))}");
            AssertInvariants(result.Value, attacker, defender);
        }
    }

    private static void AssertInvariants(BattleReport report, Player attacker, Player defender)
    {
        report.Turns.ShouldBe(report.Events.Count);
        report.Turns.ShouldBeGreaterThan(0);
        report.WinnerId.ShouldNotBe(report.LoserId);
        report.AttackerWon.ShouldBe(report.DefenderHpRemaining == 0);

        int attackerHp = attacker.Stats.HitPoints;
        int defenderHp = defender.Stats.HitPoints;

        foreach (var e in report.Events)
        {
            bool attackerTurn = e.AttackerId == attacker.Id;
            var current = attackerTurn ? attacker : defender;
            int currentHp = attackerTurn ? attackerHp : defenderHp;
            int otherHp = attackerTurn ? defenderHp : attackerHp;

            e.AttackerHpBefore.ShouldBe(currentHp);
            e.DefenderHpBefore.ShouldBe(otherHp);
            e.Roll.ShouldBeInRange(0, BattleEngine.RollRange - 1);
            e.DodgeChanceBasisPoints.ShouldBeInRange(0, BattleRules.Default.MaxDodgeBasisPoints);
            e.Hit.ShouldBe(e.Roll >= e.DodgeChanceBasisPoints);
            e.Damage.ShouldBe(e.Hit ? e.AttackValueUsed : 0);
            e.AttackValueUsed.ShouldBeInRange((current.Stats.Attack + 1) / 2, current.Stats.Attack);
            e.DefenderHpAfter.ShouldBe(Math.Max(0, otherHp - e.Damage));

            if (attackerTurn)
            {
                defenderHp = e.DefenderHpAfter;
            }
            else
            {
                attackerHp = e.DefenderHpAfter;
            }
        }

        report.Events[^1].DefenderHpAfter.ShouldBe(0);
        report.AttackerHpRemaining.ShouldBe(attackerHp);
        report.DefenderHpRemaining.ShouldBe(defenderHp);

        var loser = report.LoserId == attacker.Id ? attacker : defender;
        report.Loot.Percent.ShouldBeInRange(BattleRules.Default.MinLootPercent, BattleRules.Default.MaxLootPercent);
        report.Loot.Gold.ShouldBeLessThanOrEqualTo(loser.Resources.Gold);
        report.Loot.Silver.ShouldBeLessThanOrEqualTo(loser.Resources.Silver);
        report.Loot.ShouldBe(LootResult.Compute(loser.Resources, report.Loot.Percent));
    }

    private static Player RandomPlayer(Xoshiro256StarStar rng, string id) =>
        TestPlayers.Create(
            id,
            attack: rng.Roll(1, 500),
            defense: rng.Roll(0, 500),
            hitPoints: rng.Roll(1, 2_000),
            gold: rng.Roll(0, 1_000_000),
            silver: rng.Roll(0, 1_000_000));
}
