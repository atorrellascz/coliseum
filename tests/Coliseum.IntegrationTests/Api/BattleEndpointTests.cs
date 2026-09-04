using System.Net;
using System.Net.Http.Json;
using Coliseum.Contracts.Battles;
using Coliseum.Contracts.Errors;
using Coliseum.IntegrationTests.Fixtures;

namespace Coliseum.IntegrationTests.Api;

[Collection(RedisCollection.Name)]
public class BattleEndpointTests(RedisFixture redis)
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task Submit_returns_202_and_the_battle_is_readable_as_queued_by_both_participants()
    {
        using var api = new ApiFactory(redis.ConnectionString, RedisFixture.NewPrefix());
        using var service = await api.ServiceClientAsync(_ct);
        var (ata, ataClient) = await api.PlayerClientAsync(service, "Ata", _ct);
        var (bot, botClient) = await api.PlayerClientAsync(service, "Bot", _ct);

        var response = await ataClient.PostAsJsonAsync(new Uri("/battles", UriKind.Relative), new SubmitBattleRequest(bot.Player.Id), ApiFactory.Json, _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var submitted = (await response.Content.ReadFromJsonAsync<BattleSubmittedResponse>(ApiFactory.Json, _ct))!;
        submitted.Status.ShouldBe(BattleStatus.Queued);
        response.Headers.Location!.ToString().ShouldBe($"/battles/{submitted.BattleId}");

        var seenByDefender = await botClient.GetFromJsonAsync<BattleReportResponse>(new Uri($"/battles/{submitted.BattleId}", UriKind.Relative), ApiFactory.Json, _ct);
        seenByDefender!.AttackerId.ShouldBe(ata.Player.Id);
        seenByDefender.Status.ShouldBeOneOf(BattleStatus.Queued, BattleStatus.Processing, BattleStatus.Done);
    }

    [Fact]
    public async Task Player_attacking_as_someone_else_gets_403()
    {
        using var api = new ApiFactory(redis.ConnectionString, RedisFixture.NewPrefix());
        using var service = await api.ServiceClientAsync(_ct);
        var (_, ataClient) = await api.PlayerClientAsync(service, "Ata", _ct);
        var (bot, _) = await api.PlayerClientAsync(service, "Bot", _ct);
        var (victim, _) = await api.PlayerClientAsync(service, "Victim", _ct);

        var response = await ataClient.PostAsJsonAsync(new Uri("/battles", UriKind.Relative), new SubmitBattleRequest(bot.Player.Id, victim.Player.Id), ApiFactory.Json, _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await response.Content.ReadFromJsonAsync<ApiProblem>(ApiFactory.Json, _ct))!.Errors.Single().Code.ShouldBe("battle.attacker.mismatch");
    }

    [Fact]
    public async Task Non_participant_gets_404_and_bad_input_gets_400()
    {
        using var api = new ApiFactory(redis.ConnectionString, RedisFixture.NewPrefix());
        using var service = await api.ServiceClientAsync(_ct);
        var (_, ataClient) = await api.PlayerClientAsync(service, "Ata", _ct);
        var (bot, _) = await api.PlayerClientAsync(service, "Bot", _ct);
        var (_, strangerClient) = await api.PlayerClientAsync(service, "Stranger", _ct);
        var submitted = (await (await ataClient.PostAsJsonAsync(new Uri("/battles", UriKind.Relative), new SubmitBattleRequest(bot.Player.Id), ApiFactory.Json, _ct))
            .Content.ReadFromJsonAsync<BattleSubmittedResponse>(ApiFactory.Json, _ct))!;

        (await strangerClient.GetAsync(new Uri($"/battles/{submitted.BattleId}", UriKind.Relative), _ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await ataClient.PostAsJsonAsync(new Uri("/battles", UriKind.Relative), new SubmitBattleRequest("not a valid id!"), ApiFactory.Json, _ct)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ataClient.PostAsJsonAsync(new Uri("/battles", UriKind.Relative), new SubmitBattleRequest("01J00000000000000000000000"), ApiFactory.Json, _ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
