using System.Diagnostics.CodeAnalysis;

namespace Coliseum.Domain.Common;

/// <summary>
/// Shared format rule for external identifiers (<c>PlayerId</c>, <c>BattleId</c>).
/// Accepts <c>[A-Za-z0-9_-]{1,64}</c>: this covers ULIDs (26 Crockford base32 characters) and is safe to embed
/// in Redis keys, URLs and log lines without escaping. Validating here means no other layer ever has to
/// worry about key injection ("player:*", "..", spaces).
/// </summary>
internal static class Identifier
{
    public const int MaxLength = 64;

    public static bool IsValid([NotNullWhen(true)] string? value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > MaxLength)
        {
            return false;
        }

        foreach (char c in value)
        {
            bool allowed = c is (>= 'A' and <= 'Z') or (>= 'a' and <= 'z') or (>= '0' and <= '9') or '_' or '-';
            if (!allowed)
            {
                return false;
            }
        }

        return true;
    }
}
