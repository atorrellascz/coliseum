using Coliseum.Domain.Common;

namespace Coliseum.Domain.Players;

/// <summary>
/// Rules for the player name: at most 20 characters, non-empty, printable.
/// Uniqueness is a global property that needs storage, so it is enforced by the repository adapter
/// (atomic SET NX in Redis) using <see cref="Normalize"/> as the key: "Ata", "ata" and " ATA " are the same name (SUP-06).
/// </summary>
public static class PlayerName
{
    public const int MaxLength = 20;

    /// <summary>Canonical form used as the uniqueness key. Trim + upper-case invariant.</summary>
    public static string Normalize(string name) => name.Trim().ToUpperInvariant();

    /// <summary>Returns every rule the name breaks; empty when the name is acceptable.</summary>
    public static IReadOnlyList<DomainError> Validate(string? name)
    {
        var errors = new List<DomainError>();

        if (string.IsNullOrWhiteSpace(name))
        {
            errors.Add(DomainError.Validation("name", "player.name.required", "Name is required."));
            return errors;
        }

        string trimmed = name.Trim();

        if (trimmed.Length > MaxLength)
        {
            errors.Add(DomainError.Validation("name", "player.name.too_long", "Name must be at most 20 characters."));
        }

        foreach (char c in trimmed)
        {
            if (char.IsControl(c))
            {
                errors.Add(DomainError.Validation("name", "player.name.invalid_chars", "Name must not contain control characters."));
                break;
            }
        }

        return errors;
    }
}
