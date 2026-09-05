using System.Net;
using System.Net.Http.Json;
using Coliseum.Contracts.Admin;
using Coliseum.IntegrationTests.Fixtures;

namespace Coliseum.IntegrationTests.Api;

[Collection(RedisCollection.Name)]
public class AdminStatsTests(RedisFixture redis)
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task Admin_stats_require_a_service_token_and_return_the_snapshot()
    {
        using var api = new ApiFactory(redis.ConnectionString, RedisFixture.NewPrefix());
        using var anonymous = api.CreateClient();
        using var service = await api.ServiceClientAsync(_ct);
        var (_, player) = await api.PlayerClientAsync(service, "Ata", _ct);

        (await anonymous.GetAsync(new Uri("/admin/stats", UriKind.Relative), _ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await player.GetAsync(new Uri("/admin/stats", UriKind.Relative), _ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var stats = await service.GetFromJsonAsync<AdminStatsResponse>(new Uri("/admin/stats", UriKind.Relative), ApiFactory.Json, _ct);

        stats.ShouldNotBeNull();
        stats.Economy.BattlesProcessed.ShouldBe(0);
        stats.Economy.TurnBuckets.Count.ShouldBe(5);
        stats.Queue.DeadLettered.ShouldBe(0);
        stats.Top.ShouldBeEmpty();
    }
}
