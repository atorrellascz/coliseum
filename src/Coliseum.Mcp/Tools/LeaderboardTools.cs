using System.ComponentModel;
using Coliseum.Contracts.Leaderboard;
using ModelContextProtocol.Server;

namespace Coliseum.Mcp.Tools;

[McpServerToolType]
public static class LeaderboardTools
{
    [McpServerTool(Name = "get_leaderboard")]
    [Description("Ranked players by total resources stolen: rank, score and player id. Paged with offset and limit (max 100).")]
    public static Task<LeaderboardResponse> GetLeaderboard(
        ColiseumApiClient api,
        [Description("0-based offset")] int offset = 0,
        [Description("Page size, 1-100")] int limit = 10,
        CancellationToken cancellationToken = default) =>
        api.GetLeaderboardAsync(Math.Max(0, offset), Math.Clamp(limit, 1, 100), cancellationToken);
}
