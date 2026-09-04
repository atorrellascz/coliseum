using Coliseum.Domain.Common;
using Coliseum.UnitTests.Fakes;

namespace Coliseum.UnitTests.Application;

public class ListPlayersHandlerTests
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task Returns_recent_players_newest_first_with_a_default_limit()
    {
        var world = new FakeWorld();
        for (int i = 0; i < 60; i++)
        {
            world.Seed($"p{i:D2}");
        }

        var result = await world.ListPlayers.HandleAsync(null, _ct);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Count.ShouldBe(50);
        result.Value[0].Id.ShouldBe("p59");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public async Task Rejects_bad_limits(int limit)
    {
        var result = await new FakeWorld().ListPlayers.HandleAsync(limit, _ct);

        result.ErrorKind.ShouldBe(DomainErrorKind.Validation);
    }
}
