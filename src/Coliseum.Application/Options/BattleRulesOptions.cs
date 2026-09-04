using Coliseum.Domain.Battles;
using Microsoft.Extensions.Options;

namespace Coliseum.Application.Options;

/// <summary>
/// Configuration surface for <see cref="BattleRules"/> (section <c>Battle</c>). Defaults are the spec values;
/// validation runs at start-up so a bad deployment fails before it processes a single battle (PAT-06).
/// </summary>
public sealed class BattleRulesOptions
{
    public const string SectionName = "Battle";

    public int MinAttackPercent { get; set; } = BattleRules.Default.MinAttackPercent;

    public int MinLootPercent { get; set; } = BattleRules.Default.MinLootPercent;

    public int MaxLootPercent { get; set; } = BattleRules.Default.MaxLootPercent;

    public int MaxDodgeBasisPoints { get; set; } = BattleRules.Default.MaxDodgeBasisPoints;

    public int MaxTurns { get; set; } = BattleRules.Default.MaxTurns;

    public BattleRules ToRules() => new(MinAttackPercent, MinLootPercent, MaxLootPercent, MaxDodgeBasisPoints, MaxTurns);
}

/// <summary>Delegates to the domain's own rule validation so there is one definition of "valid rules".</summary>
public sealed class BattleRulesOptionsValidator : IValidateOptions<BattleRulesOptions>
{
    public ValidateOptionsResult Validate(string? name, BattleRulesOptions options)
    {
        var errors = options.ToRules().Validate();
        return errors.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(errors.Select(e => e.Message));
    }
}
