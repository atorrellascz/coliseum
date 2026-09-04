using Coliseum.Domain.Common;

namespace Coliseum.Domain.Players;

/// <summary>
/// Aggregate root for a player. Immutable: the only way to obtain one is <see cref="Create"/> (validates every rule
/// and reports all violations together) or <see cref="Rehydrate"/> (trusted storage reads, no validation).
/// Balances change through the settlement script in Redis, never by mutating this object; <see cref="WithResources"/>
/// exists for local simulations only.
/// </summary>
public sealed class Player
{
    public const int MaxDescriptionLength = 1_000;

    private Player(PlayerId id, string name, string description, Resources resources, CombatStats stats, DateTimeOffset createdAt)
    {
        Id = id;
        Name = name;
        Description = description;
        Resources = resources;
        Stats = stats;
        CreatedAt = createdAt;
    }

    public PlayerId Id { get; }

    /// <summary>Display name as entered (trimmed). Uniqueness is decided on <see cref="NormalizedName"/>.</summary>
    public string Name { get; }

    public string Description { get; }

    public Resources Resources { get; }

    public CombatStats Stats { get; }

    public DateTimeOffset CreatedAt { get; }

    /// <summary>Canonical key for the uniqueness check, see <see cref="PlayerName.Normalize"/>.</summary>
    public string NormalizedName => PlayerName.Normalize(Name);

    /// <summary>
    /// Builds a player from raw input. Every rule is checked and every violation is returned, so an API can show the
    /// complete list of problems in one round trip. The id and the timestamp come from the caller: the domain does not
    /// generate ids or read clocks (testability, determinism).
    /// </summary>
    public static Result<Player> Create(
        PlayerId id,
        string? name,
        string? description,
        long gold,
        long silver,
        int attack,
        int defense,
        int hitPoints,
        DateTimeOffset createdAt)
    {
        var errors = new List<DomainError>();
        errors.AddRange(PlayerName.Validate(name));

        if (description is not null && description.Length > MaxDescriptionLength)
        {
            errors.Add(DomainError.Validation("description", "player.description.too_long", "Description must be at most 1,000 characters."));
        }

        Result<Resources> resources = Resources.Create(gold, silver);
        errors.AddRange(resources.Errors);

        Result<CombatStats> stats = CombatStats.Create(attack, defense, hitPoints);
        errors.AddRange(stats.Errors);

        if (errors.Count > 0)
        {
            return Result.Fail<Player>(errors);
        }

        return Result.Ok(new Player(id, name!.Trim(), description ?? string.Empty, resources.Value, stats.Value, createdAt));
    }

    /// <summary>Rebuilds a player from storage without re-validating. Storage is trusted; input is not.</summary>
    public static Player Rehydrate(PlayerId id, string name, string description, Resources resources, CombatStats stats, DateTimeOffset createdAt) =>
        new(id, name, description, resources, stats, createdAt);

    /// <summary>Copy with different balances. Used by simulations (MCP what-if) and tests, never by the settlement path.</summary>
    public Player WithResources(Resources resources) =>
        new(Id, Name, Description, resources, Stats, CreatedAt);
}
