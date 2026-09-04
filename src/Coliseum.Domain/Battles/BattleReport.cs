using Coliseum.Domain.Players;

namespace Coliseum.Domain.Battles;

/// <summary>
/// Complete, self-describing outcome of a battle. Together with the two players' stats it is enough to
/// reproduce the battle (the seed is included) and to audit every turn (the event log is included).
/// The API adds a human-readable narrative on top; the domain keeps the facts.
/// </summary>
/// <param name="BattleId">Idempotency key and seed source.</param>
/// <param name="Seed">Seed actually used, derived from the id. Anyone can replay the battle with it.</param>
/// <param name="AttackerId">The initiator; always acts on turn 1.</param>
/// <param name="DefenderId">The opponent.</param>
/// <param name="WinnerId">The fighter left with hit points above zero.</param>
/// <param name="LoserId">The fighter reduced to zero hit points.</param>
/// <param name="Turns">Number of turns played (equals <c>Events.Count</c>).</param>
/// <param name="Events">One entry per turn, in order.</param>
/// <param name="Loot">Resources the winner takes from the loser.</param>
/// <param name="AttackerHpRemaining">Attacker's hit points at the end.</param>
/// <param name="DefenderHpRemaining">Defender's hit points at the end.</param>
public sealed record BattleReport(
    BattleId BattleId,
    ulong Seed,
    PlayerId AttackerId,
    PlayerId DefenderId,
    PlayerId WinnerId,
    PlayerId LoserId,
    int Turns,
    IReadOnlyList<TurnEvent> Events,
    LootResult Loot,
    int AttackerHpRemaining,
    int DefenderHpRemaining)
{
    public bool AttackerWon => WinnerId == AttackerId;
}
