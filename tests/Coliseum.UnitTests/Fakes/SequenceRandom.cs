using Coliseum.Domain.Randomness;

namespace Coliseum.UnitTests.Fakes;

/// <summary>
/// Scripted <see cref="IBattleRandom"/>: returns the given values in order, then fails loudly.
/// Lets a test force an exact hit/miss pattern instead of hunting for a seed that happens to produce it.
/// Values are clamped into the requested range. The engine lands a hit when roll &gt;= dodge chance, so a scripted
/// "9999" is a guaranteed hit and "0" a guaranteed miss whenever the defender has any defense.
/// </summary>
internal sealed class SequenceRandom(params int[] values) : IBattleRandom
{
    private readonly Queue<int> _values = new(values);

    public int Roll(int minInclusive, int maxExclusive)
    {
        if (_values.Count == 0)
        {
            throw new InvalidOperationException("SequenceRandom ran out of scripted values.");
        }

        return Math.Clamp(_values.Dequeue(), minInclusive, maxExclusive - 1);
    }
}
