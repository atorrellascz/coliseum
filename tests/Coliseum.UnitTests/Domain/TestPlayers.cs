using Coliseum.Domain.Players;

namespace Coliseum.UnitTests.Domain;

/// <summary>Builders for valid players so tests only spell out what matters to them.</summary>
internal static class TestPlayers
{
    public static readonly DateTimeOffset FixedDate = new(2026, 9, 3, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Spec-like defaults: 70 attack, 100 hit points, 500 gold, 120 silver.</summary>
    public static Player Create(
        string id,
        int attack = 70,
        int defense = 30,
        int hitPoints = 100,
        long gold = 500,
        long silver = 120)
    {
        var result = Player.Create(PlayerId.Unchecked(id), id, "test player", gold, silver, attack, defense, hitPoints, FixedDate);
        result.IsSuccess.ShouldBeTrue(string.Join("; ", result.Errors.Select(e => e.Code)));
        return result.Value;
    }
}
