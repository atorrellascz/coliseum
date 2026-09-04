using Coliseum.Domain.Common;
using Coliseum.Domain.Players;
using Coliseum.UnitTests.Fakes;

namespace Coliseum.UnitTests.Application;

public class GetLeaderboardHandlerTests
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task Defaults_to_first_page_of_50_with_absolute_ranks()
    {
        var world = new FakeWorld();
        for (int i = 1; i <= 60; i++)
        {
            world.Leaderboard.Add(PlayerId.Unchecked($"p{i:D2}"), i * 10);
        }

        var result = await world.GetLeaderboard.HandleAsync(null, null, _ct);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Entries.Count.ShouldBe(50);
        result.Value.Total.ShouldBe(60);
        result.Value.Entries[0].ShouldBe(new(1, 600, "p60"));
        result.Value.Entries[49].ShouldBe(new(50, 110, "p11"));
    }

    [Fact]
    public async Task Offset_pages_keep_absolute_rank()
    {
        var world = new FakeWorld();
        world.Leaderboard.Add(PlayerId.Unchecked("a"), 30);
        world.Leaderboard.Add(PlayerId.Unchecked("b"), 20);
        world.Leaderboard.Add(PlayerId.Unchecked("c"), 10);

        var result = await world.GetLeaderboard.HandleAsync(offset: 1, limit: 1, _ct);

        result.Value!.Entries.Single().ShouldBe(new(2, 20, "b"));
        result.Value.Offset.ShouldBe(1);
        result.Value.Limit.ShouldBe(1);
    }

    [Theory]
    [InlineData(-1, 10, "offset")]
    [InlineData(0, 0, "limit")]
    [InlineData(0, 101, "limit")]
    public async Task Rejects_bad_paging(int offset, int limit, string field)
    {
        var world = new FakeWorld();

        var result = await world.GetLeaderboard.HandleAsync(offset, limit, _ct);

        result.ErrorKind.ShouldBe(DomainErrorKind.Validation);
        result.Errors.Single().Field.ShouldBe(field);
    }
}
