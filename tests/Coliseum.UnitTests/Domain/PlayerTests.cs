using Coliseum.Domain.Common;
using Coliseum.Domain.Players;

namespace Coliseum.UnitTests.Domain;

/// <summary>Boundary tests for the player aggregate and its value objects: every limit of the spec, on both sides.</summary>
public class PlayerTests
{
    private static readonly PlayerId Id = PlayerId.Unchecked("01J0000000000000000000000A");

    private static Result<Player> Create(
        string? name = "Ata",
        string? description = "d",
        long gold = 500,
        long silver = 120,
        int attack = 70,
        int defense = 30,
        int hitPoints = 100) =>
        Player.Create(Id, name, description, gold, silver, attack, defense, hitPoints, TestPlayers.FixedDate);

    [Fact]
    public void Valid_input_creates_a_player_with_trimmed_name()
    {
        var result = Create(name: "  Ata  ");

        result.IsSuccess.ShouldBeTrue();
        result.Value.Name.ShouldBe("Ata");
        result.Value.NormalizedName.ShouldBe("ATA");
        result.Value.Resources.ShouldBe(Resources.Unchecked(500, 120));
        result.Value.Stats.ShouldBe(CombatStats.Unchecked(70, 30, 100));
        result.Value.CreatedAt.ShouldBe(TestPlayers.FixedDate);
    }

    [Theory]
    [InlineData(20, true)]
    [InlineData(21, false)]
    public void Name_max_length_is_20(int length, bool valid)
    {
        var result = Create(name: new string('a', length));

        result.IsSuccess.ShouldBe(valid);
        if (!valid)
        {
            result.Errors.ShouldContain(e => e.Code == "player.name.too_long" && e.Field == "name");
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Name_is_required(string? name)
    {
        var result = Create(name: name);

        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldContain(e => e.Code == "player.name.required");
    }

    [Fact]
    public void Name_rejects_control_characters()
    {
        Create(name: "Ata\nX").Errors.ShouldContain(e => e.Code == "player.name.invalid_chars");
    }

    [Theory]
    [InlineData(1000, true)]
    [InlineData(1001, false)]
    public void Description_max_length_is_1000(int length, bool valid)
    {
        Create(description: new string('x', length)).IsSuccess.ShouldBe(valid);
    }

    [Fact]
    public void Description_is_optional()
    {
        var result = Create(description: null);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Description.ShouldBe(string.Empty);
    }

    [Theory]
    [InlineData(1_000_000_000, true)]
    [InlineData(1_000_000_001, false)]
    [InlineData(0, true)]
    [InlineData(-1, false)]
    public void Gold_and_silver_are_within_0_and_1_billion(long amount, bool valid)
    {
        Create(gold: amount).IsSuccess.ShouldBe(valid);
        Create(silver: amount).IsSuccess.ShouldBe(valid);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(10_000, true)]
    [InlineData(10_001, false)]
    public void Attack_and_hit_points_are_within_1_and_max_stat(int value, bool valid)
    {
        Create(attack: value).IsSuccess.ShouldBe(valid);
        Create(hitPoints: value).IsSuccess.ShouldBe(valid);
    }

    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, true)]
    [InlineData(10_000, true)]
    [InlineData(10_001, false)]
    public void Defense_may_be_zero(int value, bool valid)
    {
        Create(defense: value).IsSuccess.ShouldBe(valid);
    }

    [Fact]
    public void All_errors_are_reported_together()
    {
        var result = Create(name: new string('n', 25), description: new string('d', 1001), gold: -1, silver: -1, attack: 0, defense: -1, hitPoints: 0);

        result.IsFailure.ShouldBeTrue();
        result.Errors.Select(e => e.Field).ShouldBe(["name", "description", "gold", "silver", "attack", "defense", "hitPoints"]);
        result.ErrorKind.ShouldBe(DomainErrorKind.Validation);
    }

    [Fact]
    public void Resources_arithmetic_saturates_at_the_caps()
    {
        var nearMax = Resources.Unchecked(Resources.MaxPerResource - 1, 0);

        nearMax.Plus(Resources.Unchecked(10, 10)).ShouldBe(Resources.Unchecked(Resources.MaxPerResource, 10));
        Resources.Unchecked(5, 5).Minus(Resources.Unchecked(10, 1)).ShouldBe(Resources.Unchecked(0, 4));
    }

    [Fact]
    public void Resources_percent_rounds_up_each_resource_individually()
    {
        // Spec example: 500 gold and 120 silver at 7% -> 35 gold and 9 silver.
        Resources.Unchecked(500, 120).Percent(7).ShouldBe(Resources.Unchecked(35, 9));
        Resources.Unchecked(1, 0).Percent(5).ShouldBe(Resources.Unchecked(1, 0));
        Resources.Zero.Percent(10).ShouldBe(Resources.Zero);
    }

    [Theory]
    [InlineData("01J0000000000000000000000A", true)]
    [InlineData("a_b-c", true)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("has space", false)]
    [InlineData("player:*", false)]
    public void Player_id_accepts_only_safe_characters(string? value, bool valid)
    {
        PlayerId.Create(value).IsSuccess.ShouldBe(valid);
    }

    [Fact]
    public void Player_id_rejects_more_than_64_characters()
    {
        PlayerId.Create(new string('a', 64)).IsSuccess.ShouldBeTrue();
        PlayerId.Create(new string('a', 65)).IsSuccess.ShouldBeFalse();
    }
}
