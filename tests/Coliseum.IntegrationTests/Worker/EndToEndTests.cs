using System.Net.Http.Json;
using Coliseum.Contracts.Battles;
using Coliseum.Contracts.Leaderboard;
using Coliseum.IntegrationTests.Fixtures;

namespace Coliseum.IntegrationTests.Worker;

/// <summary>
/// API + worker + Redis together: submit many battles among a few players, wait for all of them, and check the
/// accounting closes: every battle done exactly once, leaderboard total equals the sum of transferred loot.
/// </summary>
[Collection(RedisCollection.Name)]
public class EndToEndTests(RedisFixture redis)
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task Battles_are_processed_exactly_once_and_the_leaderboard_matches_the_loot()
    {
        string prefix = RedisFixture.NewPrefix();
        using var api = new ApiFactory(redis.ConnectionString, prefix);
        using var worker = new WorkerFactory(redis.ConnectionString, prefix);
        using var service = await api.ServiceClientAsync(_ct);
        _ = worker.CreateClient(); // starts the worker host (and its processing loop)

        var players = new List<(string Id, HttpClient Client)>();
        foreach (string name in new[] { "Ata", "Bot", "Cleo" })
        {
            var (created, client) = await api.PlayerClientAsync(service, name, _ct, gold: 10_000, silver: 5_000);
            players.Add((created.Player.Id, client));
        }

        var battleIds = new List<string>();
        for (int i = 0; i < 12; i++)
        {
            var attacker = players[i % 3];
            var defender = players[(i + 1) % 3];
            var response = await attacker.Client.PostAsJsonAsync(new Uri("/battles", UriKind.Relative), new SubmitBattleRequest(defender.Id), ApiFactory.Json, _ct);
            response.EnsureSuccessStatusCode();
            battleIds.Add((await response.Content.ReadFromJsonAsync<BattleSubmittedResponse>(ApiFactory.Json, _ct))!.BattleId);
        }

        var reports = new List<BattleReportResponse>();
        foreach (string id in battleIds)
        {
            reports.Add(await WaitForDoneAsync(service, id));
        }

        reports.ShouldAllBe(r => r.Status == BattleStatus.Done && r.Loot != null && r.Narrative != null && r.Events != null);
        reports.Select(r => r.BattleId).Distinct().Count().ShouldBe(12);

        var leaderboard = (await service.GetFromJsonAsync<LeaderboardResponse>(new Uri("/leaderboard", UriKind.Relative), ApiFactory.Json, _ct))!;
        leaderboard.Entries.Sum(e => e.Score).ShouldBe(reports.Sum(r => r.Loot!.Score));
        leaderboard.Entries.Select(e => e.Rank).ShouldBe(Enumerable.Range(1, leaderboard.Entries.Count));
    }

    private async Task<BattleReportResponse> WaitForDoneAsync(HttpClient client, string battleId)
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (DateTime.UtcNow < deadline)
        {
            var report = (await client.GetFromJsonAsync<BattleReportResponse>(new Uri($"/battles/{battleId}", UriKind.Relative), ApiFactory.Json, _ct))!;
            if (report.Status is BattleStatus.Done or BattleStatus.Failed)
            {
                return report;
            }

            await Task.Delay(100, _ct);
        }

        throw new TimeoutException($"Battle {battleId} was not processed in time.");
    }
}
