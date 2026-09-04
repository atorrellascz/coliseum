using Coliseum.Application.Ports;
using Coliseum.Application.Scheduling;
using Coliseum.Domain.Battles;
using Coliseum.Domain.Players;
using Coliseum.Domain.Randomness;

namespace Coliseum.UnitTests.Application;

/// <summary>The three guarantees of the scheduler, each with a hand-built scenario, plus a randomized simulation.</summary>
public class BattleSchedulerTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 4, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Battles_with_disjoint_players_start_together()
    {
        var scheduler = new BattleScheduler(maxConcurrency: 8);
        scheduler.Enqueue(Battle(1, "A", "B"), Now);
        scheduler.Enqueue(Battle(2, "C", "D"), Now);
        scheduler.Enqueue(Battle(3, "E", "F"), Now);

        var started = scheduler.Dispatch();

        started.Select(b => b.MessageId).ShouldBe(["1", "2", "3"]);
        scheduler.RunningCount.ShouldBe(3);
        scheduler.PendingCount.ShouldBe(0);
    }

    [Fact]
    public void Battle_sharing_a_player_waits_until_the_running_one_completes()
    {
        var scheduler = new BattleScheduler(8);
        scheduler.Enqueue(Battle(1, "A", "B"), Now);
        scheduler.Enqueue(Battle(2, "B", "C"), Now);

        scheduler.Dispatch().Select(b => b.MessageId).ShouldBe(["1"]);
        scheduler.Dispatch().ShouldBeEmpty();
        scheduler.IsBusy(PlayerId.Unchecked("B")).ShouldBeTrue();

        scheduler.Complete("1").ShouldBeTrue();

        scheduler.Dispatch().Select(b => b.MessageId).ShouldBe(["2"]);
        scheduler.IsBusy(PlayerId.Unchecked("A")).ShouldBeFalse();
    }

    [Fact]
    public void A_blocked_battle_reserves_its_players_so_later_battles_cannot_overtake_it()
    {
        // #1 A-B runs. #2 B-C is blocked by B. #3 C-D has free players but must NOT start: C is reserved by #2,
        // otherwise C would fight D before fighting B, breaking per-player submission order.
        var scheduler = new BattleScheduler(8);
        scheduler.Enqueue(Battle(1, "A", "B"), Now);
        scheduler.Enqueue(Battle(2, "B", "C"), Now);
        scheduler.Enqueue(Battle(3, "C", "D"), Now);
        scheduler.Enqueue(Battle(4, "E", "F"), Now);

        scheduler.Dispatch().Select(b => b.MessageId).ShouldBe(["1", "4"]);

        scheduler.Complete("1");
        scheduler.Dispatch().Select(b => b.MessageId).ShouldBe(["2"]);

        scheduler.Complete("2");
        scheduler.Dispatch().Select(b => b.MessageId).ShouldBe(["3"]);
    }

    [Fact]
    public void Concurrency_is_bounded()
    {
        var scheduler = new BattleScheduler(maxConcurrency: 2);
        scheduler.Enqueue(Battle(1, "A", "B"), Now);
        scheduler.Enqueue(Battle(2, "C", "D"), Now);
        scheduler.Enqueue(Battle(3, "E", "F"), Now);

        scheduler.Dispatch().Select(b => b.MessageId).ShouldBe(["1", "2"]);
        scheduler.Dispatch().ShouldBeEmpty();

        scheduler.Complete("2");
        scheduler.Dispatch().Select(b => b.MessageId).ShouldBe(["3"]);
    }

    [Fact]
    public void Completing_an_unknown_battle_is_reported_not_thrown()
    {
        new BattleScheduler(1).Complete("nope").ShouldBeFalse();
    }

    [Fact]
    public void Random_simulation_never_overlaps_players_and_keeps_per_player_order()
    {
        const int players = 12;
        const int battles = 400;
        var rng = new Xoshiro256StarStar(4242);
        var scheduler = new BattleScheduler(maxConcurrency: 6);

        var submissionOrder = new Dictionary<PlayerId, List<string>>();
        var startOrder = new Dictionary<PlayerId, List<string>>();
        var running = new Dictionary<string, QueuedBattle>(StringComparer.Ordinal);
        int submitted = 0;
        int completed = 0;

        while (completed < battles)
        {
            // Submit a random burst.
            int burst = Math.Min(rng.Roll(0, 5), battles - submitted);
            for (int i = 0; i < burst; i++)
            {
                int a = rng.Roll(0, players);
                int d = (a + rng.Roll(1, players)) % players;
                var battle = Battle(++submitted, $"P{a}", $"P{d}");
                scheduler.Enqueue(battle, Now);
                Track(submissionOrder, battle);
            }

            // Dispatch and check the "no shared player" invariant against everything currently running.
            foreach (var started in scheduler.Dispatch())
            {
                var message = started.Message;
                running.Values.ShouldAllBe(r => r.AttackerId != message.AttackerId && r.DefenderId != message.AttackerId
                                             && r.AttackerId != message.DefenderId && r.DefenderId != message.DefenderId);
                running[message.MessageId] = message;
                Track(startOrder, message);
            }

            running.Count.ShouldBeLessThanOrEqualTo(scheduler.MaxConcurrency);

            // Complete a random subset of what is running.
            foreach (var id in running.Keys.Where(_ => rng.Roll(0, 2) == 0).ToList())
            {
                scheduler.Complete(id).ShouldBeTrue();
                running.Remove(id);
                completed++;
            }

            if (submitted == battles && running.Count == 0 && scheduler.PendingCount == 0)
            {
                break;
            }
        }

        completed.ShouldBe(battles);
        foreach (var (player, order) in submissionOrder)
        {
            startOrder[player].ShouldBe(order, $"player {player} started battles out of submission order");
        }
    }

    private static void Track(Dictionary<PlayerId, List<string>> map, QueuedBattle battle)
    {
        foreach (var id in new[] { battle.AttackerId, battle.DefenderId })
        {
            if (!map.TryGetValue(id, out var list))
            {
                map[id] = list = [];
            }

            list.Add(battle.MessageId);
        }
    }

    private static QueuedBattle Battle(int id, string attacker, string defender) =>
        new(id.ToString(System.Globalization.CultureInfo.InvariantCulture), BattleId.Unchecked($"b{id}"), PlayerId.Unchecked(attacker), PlayerId.Unchecked(defender), Now, 1);
}
