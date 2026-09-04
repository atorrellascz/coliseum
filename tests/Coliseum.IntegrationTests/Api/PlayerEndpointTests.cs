using System.Net;
using System.Net.Http.Json;
using Coliseum.Contracts.Errors;
using Coliseum.Contracts.Players;
using Coliseum.IntegrationTests.Fixtures;

namespace Coliseum.IntegrationTests.Api;

[Collection(RedisCollection.Name)]
public class PlayerEndpointTests(RedisFixture redis)
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task Create_returns_201_with_location_player_and_token_then_get_reads_it_back()
    {
        using var api = new ApiFactory(redis.ConnectionString, RedisFixture.NewPrefix());
        using var service = await api.ServiceClientAsync(_ct);

        var response = await service.PostAsJsonAsync(new Uri("/players", UriKind.Relative), new CreatePlayerRequest("Ata", "the first", 500, 120, 70, 30, 100), ApiFactory.Json, _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = (await response.Content.ReadFromJsonAsync<CreatePlayerResponse>(ApiFactory.Json, _ct))!;
        response.Headers.Location!.ToString().ShouldBe($"/players/{created.Player.Id}");
        created.Player.Id.Length.ShouldBe(26);
        created.AccessToken.ShouldNotBeNullOrEmpty();

        var fetched = await service.GetFromJsonAsync<PlayerResponse>(new Uri($"/players/{created.Player.Id}", UriKind.Relative), ApiFactory.Json, _ct);
        fetched.ShouldBe(created.Player);
    }

    [Fact]
    public async Task Invalid_input_returns_400_problem_with_every_error()
    {
        using var api = new ApiFactory(redis.ConnectionString, RedisFixture.NewPrefix());
        using var service = await api.ServiceClientAsync(_ct);

        var response = await service.PostAsJsonAsync(new Uri("/players", UriKind.Relative), new CreatePlayerRequest("", null, -1, 0, 0, 0, 0), ApiFactory.Json, _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        var problem = (await response.Content.ReadFromJsonAsync<ApiProblem>(ApiFactory.Json, _ct))!;
        problem.Status.ShouldBe(400);
        problem.Errors.Select(e => e.Field).ShouldBe(["name", "gold", "attack", "hitPoints"]);
    }

    [Fact]
    public async Task Duplicate_name_returns_409()
    {
        using var api = new ApiFactory(redis.ConnectionString, RedisFixture.NewPrefix());
        using var service = await api.ServiceClientAsync(_ct);
        await api.PlayerClientAsync(service, "Ata", _ct);

        var response = await service.PostAsJsonAsync(new Uri("/players", UriKind.Relative), new CreatePlayerRequest("ATA", null, 1, 1, 1, 0, 1), ApiFactory.Json, _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.Content.ReadFromJsonAsync<ApiProblem>(ApiFactory.Json, _ct))!.Errors.Single().Code.ShouldBe("player.name.taken");
    }

    [Fact]
    public async Task Unknown_player_returns_404()
    {
        using var api = new ApiFactory(redis.ConnectionString, RedisFixture.NewPrefix());
        using var service = await api.ServiceClientAsync(_ct);

        (await service.GetAsync(new Uri("/players/01J00000000000000000000000", UriKind.Relative), _ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
