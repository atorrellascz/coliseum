using System.Text;

namespace Coliseum.Domain.Randomness;

/// <summary>
/// Turns a battle id into the 64-bit seed of its random sequence with FNV-1a (64-bit).
/// Chosen because it is trivial to re-implement anywhere (a Unity client, a Lua script, a notebook), has no
/// dependencies and is stable forever. Collision resistance is irrelevant here: the seed only needs to be a
/// deterministic function of the id.
/// </summary>
public static class SeedDerivation
{
    private const ulong OffsetBasis = 0xCBF29CE484222325UL;
    private const ulong Prime = 0x00000100000001B3UL;

    /// <summary>FNV-1a over the UTF-8 bytes of <paramref name="text"/>.</summary>
    public static ulong FromString(string text)
    {
        ulong hash = OffsetBasis;

        foreach (byte b in Encoding.UTF8.GetBytes(text))
        {
            hash ^= b;
            hash = unchecked(hash * Prime);
        }

        return hash;
    }
}
