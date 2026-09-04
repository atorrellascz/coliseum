using Coliseum.Domain.Common;

namespace Coliseum.Domain.Battles;

/// <summary>
/// Strongly-typed battle identifier. It doubles as the idempotency key of the settlement (ADR-03) and as the
/// seed source of the battle's random sequence (ADR-04): the id alone is enough to replay the battle.
/// </summary>
public readonly record struct BattleId
{
    private BattleId(string value) => Value = value;

    public string Value { get; }

    /// <summary>Validates external input (route values, queue messages from untrusted producers).</summary>
    public static Result<BattleId> Create(string? value) =>
        Identifier.IsValid(value)
            ? Result.Ok(new BattleId(value))
            : Result.Fail<BattleId>(DomainError.Validation("battleId", "battle.id.invalid", "Battle id must be 1-64 characters from [A-Za-z0-9_-]."));

    /// <summary>Wraps a value already known to be valid (generated ids, storage reads). Never use it for user input.</summary>
    public static BattleId Unchecked(string value) => new(value);

    public override string ToString() => Value ?? string.Empty;
}
