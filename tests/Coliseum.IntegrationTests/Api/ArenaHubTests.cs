using System.Net.Http.Json;
using System.Text.Json;
using Coliseum.Api.Hubs;
using Coliseum.Contracts.Battles;
using Coliseum.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;

namespace Coliseum.IntegrationTests.Api;

/// <summary>
/// Live feed end to end: a player connects with its token, a battle is submitted, the worker settles it,
/// the worker publishes on Redis, the API relays to the player's group, the client receives turns then done.
/// </summary>
[Collection(RedisCollection.Name)]
public class ArenaHubTests(RedisFixture redis)
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task Player_receives_its_own_turns_and_outcome_over_the_hub()
    {
        string prefix = RedisFixture.NewPrefix();
        using var api = new ApiFactory(redis.ConnectionString, prefix);
        using var worker = new WorkerFactory(redis.ConnectionString, prefix);
        using var service = await api.ServiceClientAsync(_ct);
        _ = worker.CreateClient();
        var (ata, ataClient) = await api.PlayerClientAsync(service, "Ata", _ct);
        var (bot, _) = await api.PlayerClientAsync(service, "Bot", _ct);

        var received = new List<JsonElement>();
        var done = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var connection = new HubConnectionBuilder()
            .WithUrl(api.Server.BaseAddress + "hubs/arena", options =>
            {
                options.HttpMessageHandlerFactory = _ => api.Server.CreateHandler();
                options.AccessTokenProvider = () => Task.FromResult<string?>(ata.AccessToken);
                options.Transports = HttpTransportType.LongPolling; // the in-memory TestServer has no WebSockets
            })
            .Build();
        connection.On<string>(ArenaHub.EventMethod, json =>
        {
            var element = JsonDocument.Parse(json).RootElement;
            lock (received)
            {
                received.Add(element);
            }

            if (element.GetProperty("type").GetString() == "battle.done")
            {
                done.TrySetResult(element);
            }
        });
        await connection.StartAsync(_ct);

        var submitted = await (await ataClient.PostAsJsonAsync(new Uri("/battles", UriKind.Relative), new SubmitBattleRequest(bot.Player.Id), ApiFactory.Json, _ct))
            .Content.ReadFromJsonAsync<BattleSubmittedResponse>(ApiFactory.Json, _ct);

        var outcome = await done.Task.WaitAsync(TimeSpan.FromSeconds(30), _ct);

        outcome.GetProperty("battleId").GetString().ShouldBe(submitted!.BattleId);
        outcome.GetProperty("attackerId").GetString().ShouldBe(ata.Player.Id);

        List<string> types;
        lock (received)
        {
            types = received.Select(e => e.GetProperty("type").GetString()!).ToList();
        }

        types.ShouldContain("battle.queued");
        types.ShouldContain("battle.turn");
        types.IndexOf("battle.done").ShouldBeGreaterThan(types.LastIndexOf("battle.turn"));
    }

    [Fact]
    public async Task Connection_without_a_token_is_rejected()
    {
        using var api = new ApiFactory(redis.ConnectionString, RedisFixture.NewPrefix());

        await using var connection = new HubConnectionBuilder()
            .WithUrl(api.Server.BaseAddress + "hubs/arena", options =>
            {
                options.HttpMessageHandlerFactory = _ => api.Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
            })
            .Build();

        await Should.ThrowAsync<HttpRequestException>(() => connection.StartAsync(_ct));
    }
}
