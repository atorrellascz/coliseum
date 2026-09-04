using Coliseum.Application.Ports;
using Coliseum.Domain.Players;
using Coliseum.IntegrationTests.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace Coliseum.IntegrationTests.Redis;

[Collection(RedisCollection.Name)]
public class LeaderboardTests(RedisFixture redis)
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task Ranks_pages_and_ties_are_deterministic()
    {
        string prefix = RedisFixture.NewPrefix();
        using var services = redis.BuildServices(prefix);
        var leaderboard = services.GetRequiredService<ILeaderboard>();
        var db = redis.Multiplexer.GetDatabase();
        var key = RedisFixture.KeysFor(prefix).Leaderboard;
        await db.SortedSetAddAsync(key, [new("alice", 300), new("bob", 200), new("carol", 200), new("dave", 50)]);

        var top = await leaderboard.GetTopAsync(0, 10, _ct);
        top.Select(e => (e.Rank, e.Score, e.PlayerId)).ShouldBe([(1, 300L, "alice"), (2, 200L, "carol"), (3, 200L, "bob"), (4, 50L, "dave")]);

        var page = await leaderboard.GetTopAsync(1, 2, _ct);
        page.Select(e => e.Rank).ShouldBe([2, 3]);

        (await leaderboard.CountAsync(_ct)).ShouldBe(4);
        (await leaderboard.GetEntryAsync(PlayerId.Unchecked("bob"), _ct)).ShouldBe(new(3, 200, "bob"));
        (await leaderboard.GetEntryAsync(PlayerId.Unchecked("nobody"), _ct)).ShouldBeNull();
        (await leaderboard.GetTopAsync(10, 5, _ct)).ShouldBeEmpty();
    }
}
