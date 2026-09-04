using System.Net;
using System.Net.Http.Json;
using Coliseum.Contracts.Leaderboard;
using Coliseum.IntegrationTests.Fixtures;

namespace Coliseum.IntegrationTests.Api;

[Collection(RedisCollection.Name)]
public class LeaderboardEndpointTests(RedisFixture redis)
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task Returns_rank_score_and_player_id_with_paging_metadata()
    {
        string prefix = RedisFixture.NewPrefix();
        using var api = new ApiFactory(redis.ConnectionString, prefix);
        using var service = await api.ServiceClientAsync(_ct);
        await redis.Multiplexer.GetDatabase().SortedSetAddAsync(RedisFixture.KeysFor(prefix).Leaderboard, [new("alice", 44), new("bob", 12)]);

        var page = await service.GetFromJsonAsync<LeaderboardResponse>(new Uri("/leaderboard?offset=0&limit=1", UriKind.Relative), ApiFactory.Json, _ct);

        page!.Entries.Single().ShouldBe(new LeaderboardEntry(1, 44, "alice"));
        page.Total.ShouldBe(2);
        page.Limit.ShouldBe(1);
    }

    [Fact]
    public async Task Limit_above_100_is_rejected()
    {
        using var api = new ApiFactory(redis.ConnectionString, RedisFixture.NewPrefix());
        using var service = await api.ServiceClientAsync(_ct);

        (await service.GetAsync(new Uri("/leaderboard?limit=101", UriKind.Relative), _ct)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }
}
