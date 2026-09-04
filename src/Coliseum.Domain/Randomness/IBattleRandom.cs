namespace Coliseum.Domain.Randomness;

/// <summary>
/// Source of randomness injected into the battle engine (Strategy, PAT-05). Production uses
/// <see cref="Xoshiro256StarStar"/> seeded from the battle id; tests inject scripted sequences to force
/// specific hit/miss paths. The engine never touches <c>System.Random</c> (ADR-04): its algorithm is not
/// guaranteed stable across .NET versions, which would silently break replays and golden tests.
/// </summary>
public interface IBattleRandom
{
    /// <summary>Uniform integer in [<paramref name="minInclusive"/>, <paramref name="maxExclusive"/>).</summary>
    int Roll(int minInclusive, int maxExclusive);
}
