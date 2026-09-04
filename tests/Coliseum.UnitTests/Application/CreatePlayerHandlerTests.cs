using Coliseum.Contracts.Players;
using Coliseum.Domain.Common;
using Coliseum.UnitTests.Fakes;

namespace Coliseum.UnitTests.Application;

public class CreatePlayerHandlerTests
{
    private static readonly CreatePlayerRequest Valid = new("Ata", "the first", 500, 120, 70, 30, 100);

    [Fact]
    public async Task Valid_request_stores_the_player_and_returns_a_player_token()
    {
        var world = new FakeWorld();

        var result = await world.CreatePlayer.HandleAsync(Valid, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Player.Id.ShouldBe("id-0001");
        result.Value.Player.Name.ShouldBe("Ata");
        result.Value.Player.CreatedAt.ShouldBe(world.Clock.UtcNow);
        result.Value.AccessToken.ShouldBe("token:Player:id-0001");
        world.Players.All.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Invalid_request_returns_every_violation_and_stores_nothing()
    {
        var world = new FakeWorld();
        var request = new CreatePlayerRequest(new string('x', 21), null, -1, 0, 0, 0, 0);

        var result = await world.CreatePlayer.HandleAsync(request, TestContext.Current.CancellationToken);

        result.ErrorKind.ShouldBe(DomainErrorKind.Validation);
        result.Errors.Select(e => e.Field).ShouldBe(["name", "gold", "attack", "hitPoints"]);
        world.Players.All.ShouldBeEmpty();
        world.Log.ShouldNotContain("players:create");
    }

    [Fact]
    public async Task Duplicate_name_is_a_conflict_regardless_of_case_and_spacing()
    {
        var world = new FakeWorld();
        (await world.CreatePlayer.HandleAsync(Valid, TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();

        var result = await world.CreatePlayer.HandleAsync(Valid with { Name = "  ATA " }, TestContext.Current.CancellationToken);

        result.ErrorKind.ShouldBe(DomainErrorKind.Conflict);
        result.Errors.Single().Code.ShouldBe("player.name.taken");
        world.Players.All.Count.ShouldBe(1);
    }
}
