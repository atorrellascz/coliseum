using Coliseum.Domain.Randomness;

namespace Coliseum.UnitTests.Domain;

/// <summary>
/// Known-answer tests: the generator, the seed expander and the id hash are pinned to their published reference
/// outputs. If any of them drifts, every golden report would silently change; these tests name the culprit.
/// </summary>
public class Xoshiro256StarStarTests
{
    [Fact]
    public void Matches_the_reference_sequence_for_state_1_2_3_4()
    {
        var rng = new Xoshiro256StarStar(1, 2, 3, 4);

        rng.NextRaw().ShouldBe(11520UL);
        rng.NextRaw().ShouldBe(0UL);
        rng.NextRaw().ShouldBe(1509978240UL);
        rng.NextRaw().ShouldBe(1215971899390074240UL);
    }

    [Fact]
    public void SplitMix64_matches_the_reference_for_seed_zero()
    {
        ulong state = 0;

        Xoshiro256StarStar.SplitMix64(ref state).ShouldBe(0xE220A8397B1DCDAFUL);
        Xoshiro256StarStar.SplitMix64(ref state).ShouldBe(0x6E789E6AA1B965F4UL);
    }

    [Theory]
    [InlineData("", 0xCBF29CE484222325UL)]
    [InlineData("a", 0xAF63DC4C8601EC8CUL)]
    [InlineData("foobar", 0x85944171F73967E8UL)]
    public void Seed_derivation_is_fnv1a_64(string text, ulong expected)
    {
        SeedDerivation.FromString(text).ShouldBe(expected);
    }

    [Fact]
    public void Same_seed_gives_the_same_sequence()
    {
        var a = new Xoshiro256StarStar(42);
        var b = new Xoshiro256StarStar(42);

        Enumerable.Range(0, 100).Select(_ => a.NextRaw()).ShouldBe(Enumerable.Range(0, 100).Select(_ => b.NextRaw()));
    }

    [Fact]
    public void Roll_stays_within_bounds_and_reaches_both_ends()
    {
        var rng = new Xoshiro256StarStar(7);
        var seen = new HashSet<int>();

        for (int i = 0; i < 10_000; i++)
        {
            int value = rng.Roll(5, 11);
            value.ShouldBeInRange(5, 10);
            seen.Add(value);
        }

        seen.ShouldBe([5, 6, 7, 8, 9, 10], ignoreOrder: true);
    }

    [Fact]
    public void Roll_with_a_single_outcome_returns_it_without_consuming_luck()
    {
        new Xoshiro256StarStar(1).Roll(3, 4).ShouldBe(3);
    }

    [Fact]
    public void Roll_rejects_an_empty_range()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new Xoshiro256StarStar(1).Roll(5, 5));
    }
}
