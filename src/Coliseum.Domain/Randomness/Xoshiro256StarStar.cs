using System.Numerics;

namespace Coliseum.Domain.Randomness;

/// <summary>
/// xoshiro256** (Blackman &amp; Vigna, 2018): fast, tiny state, excellent statistical quality, and a fixed public
/// algorithm so a seed produces the same sequence forever, on every platform and in any language that implements it.
/// The 64-bit seed is expanded into the 256-bit state with SplitMix64, as the authors recommend.
/// Not a cryptographic generator, and it does not need to be: the seed is public (it is derived from the battle id)
/// and reproducibility is the feature.
/// </summary>
public sealed class Xoshiro256StarStar : IBattleRandom
{
    private ulong _s0;
    private ulong _s1;
    private ulong _s2;
    private ulong _s3;

    /// <summary>Creates a generator whose whole future is determined by <paramref name="seed"/>.</summary>
    public Xoshiro256StarStar(ulong seed)
    {
        ulong state = seed;
        _s0 = SplitMix64(ref state);
        _s1 = SplitMix64(ref state);
        _s2 = SplitMix64(ref state);
        _s3 = SplitMix64(ref state);
    }

    /// <summary>Raw-state constructor for known-answer tests against the reference implementation.</summary>
    internal Xoshiro256StarStar(ulong s0, ulong s1, ulong s2, ulong s3)
    {
        _s0 = s0;
        _s1 = s1;
        _s2 = s2;
        _s3 = s3;
    }

    /// <summary>Full 64-bit output of the generator.</summary>
    public ulong NextRaw()
    {
        ulong result = BitOperations.RotateLeft(_s1 * 5, 7) * 9;
        ulong t = _s1 << 17;

        _s2 ^= _s0;
        _s3 ^= _s1;
        _s1 ^= _s2;
        _s0 ^= _s3;
        _s2 ^= t;
        _s3 = BitOperations.RotateLeft(_s3, 45);

        return result;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Unbiased bounded integer via Lemire's multiply-then-reject method: a plain modulo would favour the low values
    /// whenever the range does not divide 2^64, and the dodge roll must be exactly uniform over 10,000 outcomes.
    /// </remarks>
    public int Roll(int minInclusive, int maxExclusive)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(minInclusive, maxExclusive);

        ulong range = (ulong)((long)maxExclusive - minInclusive);
        ulong high = Math.BigMul(NextRaw(), range, out ulong low);

        if (low < range)
        {
            // 2^64 mod range, computed without 128-bit arithmetic.
            ulong threshold = unchecked(0UL - range) % range;
            while (low < threshold)
            {
                high = Math.BigMul(NextRaw(), range, out low);
            }
        }

        return (int)(minInclusive + (long)high);
    }

    /// <summary>SplitMix64 step (Steele, Lea &amp; Flood). Used only to expand the seed.</summary>
    internal static ulong SplitMix64(ref ulong state)
    {
        unchecked
        {
            ulong z = state += 0x9E3779B97F4A7C15UL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }
    }
}
