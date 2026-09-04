using System.ComponentModel;
using Coliseum.Contracts.Battles;
using ModelContextProtocol.Server;

namespace Coliseum.Mcp.Tools;

/// <summary>Battle tools. Submission is asynchronous on the API; <c>play_battle</c> hides the polling for agents that want the result.</summary>
[McpServerToolType]
public static class BattleTools
{
    [McpServerTool(Name = "submit_battle")]
    [Description("Queue a battle between two players. Returns immediately with the battle id and status 'queued'; use get_battle_report to read the outcome.")]
    public static Task<BattleSubmittedResponse> SubmitBattle(
        ColiseumApiClient api,
        [Description("Id of the player who initiates the battle (attacks first)")] string attackerId,
        [Description("Id of the opponent")] string defenderId,
        CancellationToken cancellationToken = default) =>
        api.SubmitBattleAsync(attackerId, defenderId, cancellationToken);

    [McpServerTool(Name = "get_battle_report")]
    [Description("Read a battle: status while queued or processing; once done, winner, loser, loot transferred, the turn-by-turn events and a narrative.")]
    public static Task<BattleReportResponse> GetBattleReport(
        ColiseumApiClient api,
        [Description("Battle id returned by submit_battle")] string battleId,
        CancellationToken cancellationToken = default) =>
        api.GetBattleAsync(battleId, cancellationToken);

    [McpServerTool(Name = "play_battle")]
    [Description("Submit a battle and wait for it to be processed (up to timeoutSeconds). Returns the full report with narrative.")]
    public static async Task<BattleReportResponse> PlayBattle(
        ColiseumApiClient api,
        [Description("Id of the player who initiates the battle")] string attackerId,
        [Description("Id of the opponent")] string defenderId,
        [Description("How long to wait for the worker, 1-60 seconds")] int timeoutSeconds = 15,
        CancellationToken cancellationToken = default)
    {
        var submitted = await api.SubmitBattleAsync(attackerId, defenderId, cancellationToken);
        return await api.WaitForBattleAsync(submitted.BattleId, TimeSpan.FromSeconds(Math.Clamp(timeoutSeconds, 1, 60)), cancellationToken);
    }
}
