using System.Text.Json;
using Coliseum.Domain.Battles;
using Coliseum.Domain.Players;

namespace Coliseum.RegressionTests;

/// <summary>
/// Golden-file regression tests. Ten fixed battles (fixed ids, fixed players) are serialized and compared
/// byte-for-byte with the JSON frozen under <c>golden/</c>. Any change to a rule, a formula, the PRNG or the
/// report shape fails here on purpose: game balance is a contract. When a change is intentional, regenerate with
/// <c>UPDATE_GOLDEN=1 dotnet test tests/Coliseum.RegressionTests</c> and review the diff like any other code change.
/// </summary>
public class GoldenBattleTests
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    private static readonly DateTimeOffset FixedDate = new(2026, 9, 3, 0, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(10)]
    public void Battle_report_matches_the_golden_file(int seed)
    {
        var battleId = BattleId.Unchecked($"golden-{seed:D4}");
        var attacker = FixedPlayer("attacker", attack: 40 + seed * 7, defense: 10 + seed * 5, hitPoints: 80 + seed * 15, gold: 500 * seed, silver: 120 * seed);
        var defender = FixedPlayer("defender", attack: 90 - seed * 4, defense: 60 - seed * 5, hitPoints: 200 - seed * 9, gold: 999 * seed, silver: 7 * seed);

        var result = BattleEngine.Run(battleId, attacker, defender);
        result.IsSuccess.ShouldBeTrue();

        string actual = JsonSerializer.Serialize(result.Value, Json).ReplaceLineEndings("\n");
        string goldenFile = $"{battleId.Value}.json";

        if (Environment.GetEnvironmentVariable("UPDATE_GOLDEN") == "1")
        {
            string directory = Directory.CreateDirectory(SourceGoldenDirectory()).FullName;
            File.WriteAllText(Path.Combine(directory, goldenFile), actual + "\n");
            return;
        }

        string expected = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "golden", goldenFile)).ReplaceLineEndings("\n").TrimEnd('\n');
        actual.ShouldBe(expected, $"golden file {goldenFile} differs; run with UPDATE_GOLDEN=1 if the change is intentional");
    }

    private static Player FixedPlayer(string id, int attack, int defense, int hitPoints, long gold, long silver)
    {
        var result = Player.Create(PlayerId.Unchecked(id), id, string.Empty, gold, silver, attack, defense, hitPoints, FixedDate);
        result.IsSuccess.ShouldBeTrue();
        return result.Value;
    }

    private static string SourceGoldenDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Coliseum.RegressionTests.csproj")))
        {
            dir = dir.Parent;
        }

        return Path.Combine(dir?.FullName ?? throw new InvalidOperationException("project directory not found"), "golden");
    }
}
