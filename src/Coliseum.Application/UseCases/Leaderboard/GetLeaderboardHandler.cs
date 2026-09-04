using Coliseum.Application.Ports;
using Coliseum.Contracts.Leaderboard;
using Coliseum.Domain.Common;

namespace Coliseum.Application.UseCases.Leaderboard;

/// <summary><c>GET /leaderboard</c>: offset/limit paging with a hard cap so a client cannot ask for the whole set.</summary>
public sealed class GetLeaderboardHandler(ILeaderboard leaderboard)
{
    public const int DefaultLimit = 50;
    public const int MaxLimit = 100;

    public async Task<Result<LeaderboardResponse>> HandleAsync(int? offset, int? limit, CancellationToken cancellationToken)
    {
        int effectiveOffset = offset ?? 0;
        int effectiveLimit = limit ?? DefaultLimit;
        var errors = new List<DomainError>(2);

        if (effectiveOffset < 0)
        {
            errors.Add(DomainError.Validation("offset", "leaderboard.offset.invalid", "Offset must be zero or positive."));
        }

        if (effectiveLimit is < 1 or > MaxLimit)
        {
            errors.Add(DomainError.Validation("limit", "leaderboard.limit.invalid", "Limit must be between 1 and 100."));
        }

        if (errors.Count > 0)
        {
            return Result.Fail<LeaderboardResponse>(errors);
        }

        var entries = await leaderboard.GetTopAsync(effectiveOffset, effectiveLimit, cancellationToken);
        long total = await leaderboard.CountAsync(cancellationToken);

        return Result.Ok(new LeaderboardResponse(entries, effectiveOffset, effectiveLimit, total));
    }
}
