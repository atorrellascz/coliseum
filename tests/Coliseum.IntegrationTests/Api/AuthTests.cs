using System.Net;
using System.Net.Http.Json;
using Coliseum.Api.Auth;
using Coliseum.Contracts.Players;
using Coliseum.IntegrationTests.Fixtures;

namespace Coliseum.IntegrationTests.Api;

/// <summary>REQ-16: every endpoint is protected; keys and roles behave as documented.</summary>
[Collection(RedisCollection.Name)]
public class AuthTests(RedisFixture redis)
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("GET", "/leaderboard")]
    [InlineData("GET", "/players/someone")]
    [InlineData("GET", "/battles/someone")]
    [InlineData("POST", "/players")]
    [InlineData("POST", "/battles")]
    public async Task Without_a_token_every_business_endpoint_returns_401(string method, string path)
    {
        using var api = new ApiFactory(redis.ConnectionString, RedisFixture.NewPrefix());
        using var client = api.CreateClient();

        using var request = new HttpRequestMessage(new HttpMethod(method), path) { Content = JsonContent.Create(new { }) };
        var response = await client.SendAsync(request, _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Health_and_metrics_are_anonymous()
    {
        using var api = new ApiFactory(redis.ConnectionString, RedisFixture.NewPrefix());
        using var client = api.CreateClient();

        (await client.GetAsync(new Uri("/healthz/live", UriKind.Relative), _ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await client.GetAsync(new Uri("/healthz/ready", UriKind.Relative), _ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await client.GetAsync(new Uri("/metrics", UriKind.Relative), _ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Wrong_api_key_is_rejected_and_right_key_yields_a_service_token()
    {
        using var api = new ApiFactory(redis.ConnectionString, RedisFixture.NewPrefix());
        using var anonymous = api.CreateClient();
        anonymous.DefaultRequestHeaders.Add(ApiKeyExchange.HeaderName, "nope");
        (await anonymous.PostAsync(new Uri("/auth/token", UriKind.Relative), null, _ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        using var service = await api.ServiceClientAsync(_ct);
        (await service.GetAsync(new Uri("/leaderboard", UriKind.Relative), _ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Player_token_cannot_create_players()
    {
        using var api = new ApiFactory(redis.ConnectionString, RedisFixture.NewPrefix());
        using var service = await api.ServiceClientAsync(_ct);
        var (_, player) = await api.PlayerClientAsync(service, "Ata", _ct);

        var response = await player.PostAsJsonAsync(new Uri("/players", UriKind.Relative), new CreatePlayerRequest("Bot", null, 1, 1, 1, 1, 1), ApiFactory.Json, _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
