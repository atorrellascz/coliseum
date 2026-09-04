using Coliseum.Application.Ports;
using Coliseum.Domain.Players;
using Coliseum.IntegrationTests.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace Coliseum.IntegrationTests.Redis;

[Collection(RedisCollection.Name)]
public class PlayerRepositoryTests(RedisFixture redis)
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task Create_then_get_round_trips_every_field()
    {
        using var services = redis.BuildServices(RedisFixture.NewPrefix());
        var repository = services.GetRequiredService<IPlayerRepository>();
        var player = NewPlayer("p1", "Ata");

        (await repository.CreateAsync(player, _ct)).ShouldBeTrue();
        var loaded = await repository.GetAsync(player.Id, _ct);

        loaded.ShouldNotBeNull();
        loaded.Id.ShouldBe(player.Id);
        loaded.Name.ShouldBe("Ata");
        loaded.Description.ShouldBe("the first");
        loaded.Resources.ShouldBe(player.Resources);
        loaded.Stats.ShouldBe(player.Stats);
        loaded.CreatedAt.ShouldBe(player.CreatedAt);
    }

    [Fact]
    public async Task Duplicate_name_is_rejected_regardless_of_case_and_nothing_is_written()
    {
        using var services = redis.BuildServices(RedisFixture.NewPrefix());
        var repository = services.GetRequiredService<IPlayerRepository>();

        (await repository.CreateAsync(NewPlayer("p1", "Ata"), _ct)).ShouldBeTrue();
        (await repository.CreateAsync(NewPlayer("p2", "  ata "), _ct)).ShouldBeFalse();

        (await repository.GetAsync(PlayerId.Unchecked("p2"), _ct)).ShouldBeNull();
    }

    [Fact]
    public async Task Fifty_concurrent_creations_with_the_same_name_yield_exactly_one_success()
    {
        using var services = redis.BuildServices(RedisFixture.NewPrefix());
        var repository = services.GetRequiredService<IPlayerRepository>();

        var attempts = Enumerable.Range(0, 50).Select(i => repository.CreateAsync(NewPlayer($"p{i}", "Highlander"), _ct));
        var results = await Task.WhenAll(attempts);

        results.Count(created => created).ShouldBe(1);
    }

    [Fact]
    public async Task GetMany_returns_only_existing_players_in_one_round_trip()
    {
        using var services = redis.BuildServices(RedisFixture.NewPrefix());
        var repository = services.GetRequiredService<IPlayerRepository>();
        await repository.CreateAsync(NewPlayer("a", "A"), _ct);
        await repository.CreateAsync(NewPlayer("b", "B"), _ct);

        var found = await repository.GetManyAsync([PlayerId.Unchecked("a"), PlayerId.Unchecked("ghost"), PlayerId.Unchecked("b")], _ct);

        found.Keys.Select(k => k.Value).ShouldBe(["a", "b"], ignoreOrder: true);
    }

    private static Player NewPlayer(string id, string name)
    {
        var result = Player.Create(PlayerId.Unchecked(id), name, "the first", 500, 120, 70, 30, 100, new DateTimeOffset(2026, 9, 4, 12, 0, 0, TimeSpan.Zero));
        result.IsSuccess.ShouldBeTrue();
        return result.Value;
    }
}
