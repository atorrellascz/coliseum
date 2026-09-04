using Coliseum.Application.UseCases.Battles;
using Coliseum.Application.UseCases.Leaderboard;
using Coliseum.Application.UseCases.Players;
using Coliseum.Domain.Battles;
using Coliseum.Domain.Players;
using Coliseum.UnitTests.Domain;
using Microsoft.Extensions.Logging.Abstractions;

namespace Coliseum.UnitTests.Fakes;

/// <summary>
/// Wires every fake port together the way the hosts wire the real adapters, so a use-case test reads like a
/// scenario: seed players, call a handler, inspect the world. <see cref="Log"/> records the order of port calls.
/// </summary>
internal sealed class FakeWorld
{
    public FakeWorld()
    {
        Clock = new FixedClock(new DateTimeOffset(2026, 9, 4, 10, 0, 0, TimeSpan.Zero));
        Players = new InMemoryPlayerRepository(Log);
        Reports = new InMemoryBattleReportStore(Log);
        Queue = new InMemoryBattleQueue(Clock, Log);
        Leaderboard = new InMemoryLeaderboard();
        Ledger = new InMemoryBattleLedger(Players, Leaderboard, Reports, Clock);
        Events = new RecordingEventPublisher(Log);
        Tokens = new FakeTokenService(Clock);
    }

    public List<string> Log { get; } = [];

    public FixedClock Clock { get; }

    public SequentialIdGenerator Ids { get; } = new();

    public InMemoryPlayerRepository Players { get; }

    public InMemoryBattleReportStore Reports { get; }

    public InMemoryBattleQueue Queue { get; }

    public InMemoryLeaderboard Leaderboard { get; }

    public InMemoryBattleLedger Ledger { get; }

    public RecordingEventPublisher Events { get; }

    public FakeTokenService Tokens { get; }

    public BattleRules Rules { get; set; } = BattleRules.Default;

    public CreatePlayerHandler CreatePlayer => new(Players, Ids, Clock, Tokens, NullLogger<CreatePlayerHandler>.Instance);

    public GetPlayerHandler GetPlayer => new(Players);

    public SubmitBattleHandler SubmitBattle => new(Players, Reports, Queue, Events, Ids, Clock, NullLogger<SubmitBattleHandler>.Instance);

    public GetBattleHandler GetBattle => new(Reports, Players);

    public GetLeaderboardHandler GetLeaderboard => new(Leaderboard);

    public ListPlayersHandler ListPlayers => new(Players);

    public ProcessBattleHandler ProcessBattle => new(Players, Reports, Ledger, Leaderboard, Events, Rules, Clock, NullLogger<ProcessBattleHandler>.Instance);

    public Player Seed(string id, int attack = 70, int defense = 30, int hitPoints = 100, long gold = 500, long silver = 120)
    {
        var player = TestPlayers.Create(id, attack, defense, hitPoints, gold, silver);
        Players.Seed(player);
        return player;
    }
}
