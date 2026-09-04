using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Coliseum.Contracts.Battles;
using Coliseum.Contracts.Errors;
using Coliseum.Contracts.Leaderboard;
using Coliseum.Contracts.Players;

namespace Coliseum.Mcp;

/// <summary>
/// Typed client for the Coliseum API using the shared Contracts. The MCP server is a client like any other
/// (ADR-13): it authenticates with a service token and turns Problem Details answers into readable errors
/// for the agent, including every field-level validation message.
/// </summary>
public sealed class ColiseumApiClient(HttpClient http, ServiceTokenProvider tokens, TimeProvider timeProvider)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } };

    public Task<CreatePlayerResponse> CreatePlayerAsync(CreatePlayerRequest request, CancellationToken cancellationToken) =>
        SendAsync<CreatePlayerResponse>(HttpMethod.Post, "players", request, cancellationToken);

    public Task<PlayerResponse> GetPlayerAsync(string playerId, CancellationToken cancellationToken) =>
        SendAsync<PlayerResponse>(HttpMethod.Get, $"players/{Uri.EscapeDataString(playerId)}", null, cancellationToken);

    public Task<BattleSubmittedResponse> SubmitBattleAsync(string attackerId, string defenderId, CancellationToken cancellationToken) =>
        SendAsync<BattleSubmittedResponse>(HttpMethod.Post, "battles", new SubmitBattleRequest(defenderId, attackerId), cancellationToken);

    public Task<BattleReportResponse> GetBattleAsync(string battleId, CancellationToken cancellationToken) =>
        SendAsync<BattleReportResponse>(HttpMethod.Get, $"battles/{Uri.EscapeDataString(battleId)}", null, cancellationToken);

    public Task<LeaderboardResponse> GetLeaderboardAsync(int offset, int limit, CancellationToken cancellationToken) =>
        SendAsync<LeaderboardResponse>(HttpMethod.Get, $"leaderboard?offset={offset}&limit={limit}", null, cancellationToken);

    /// <summary>Polls until the battle is settled or failed, or the timeout elapses (the API is asynchronous by design).</summary>
    public async Task<BattleReportResponse> WaitForBattleAsync(string battleId, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var deadline = timeProvider.GetUtcNow() + timeout;
        while (true)
        {
            var report = await GetBattleAsync(battleId, cancellationToken);
            if (report.Status is BattleStatus.Done or BattleStatus.Failed || timeProvider.GetUtcNow() >= deadline)
            {
                return report;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken);
        }
    }

    private async Task<T> SendAsync<T>(HttpMethod method, string path, object? body, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await tokens.GetTokenAsync(cancellationToken));
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: Json);
        }

        using var response = await http.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<T>(Json, cancellationToken)
                ?? throw new InvalidOperationException("Empty response from the Coliseum API.");
        }

        var problem = await TryReadProblemAsync(response, cancellationToken);
        string detail = problem is null
            ? $"{(int)response.StatusCode} {response.ReasonPhrase}"
            : $"{problem.Title}: {string.Join("; ", problem.Errors.Select(e => e.Field is null ? e.Message : $"{e.Field}: {e.Message}"))}";
        throw new InvalidOperationException($"Coliseum API {method} /{path} failed: {detail}");
    }

    private static async Task<ApiProblem?> TryReadProblemAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.Content.Headers.ContentType?.MediaType != "application/problem+json")
        {
            return null;
        }

        try
        {
            return await response.Content.ReadFromJsonAsync<ApiProblem>(Json, cancellationToken);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
