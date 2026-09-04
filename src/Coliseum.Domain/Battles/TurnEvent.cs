using Coliseum.Domain.Players;

namespace Coliseum.Domain.Battles;

/// <summary>
/// One entry of the battle's event log. It records every input of the turn (who, with what attack, against which
/// dodge chance, which roll) and every output (hit, damage, remaining hit points), so a report can be audited
/// line by line and a narrative can be produced without re-running the engine.
/// </summary>
/// <param name="Turn">1-based turn number.</param>
/// <param name="AttackerId">Who attacks this turn.</param>
/// <param name="DefenderId">Who defends this turn.</param>
/// <param name="AttackerHpBefore">Attacker's hit points when the turn starts (drives the attack decay).</param>
/// <param name="DefenderHpBefore">Defender's hit points when the turn starts.</param>
/// <param name="AttackValueUsed">Attack after decay, see <see cref="BattleEngine.CurrentAttack"/>.</param>
/// <param name="DodgeChanceBasisPoints">Defender's chance to dodge, 0-10,000, see <see cref="BattleEngine.DodgeBasisPoints"/>.</param>
/// <param name="Roll">Uniform roll in [0, 10,000). The attack lands when Roll &gt;= DodgeChanceBasisPoints.</param>
/// <param name="Hit">Whether the attack landed.</param>
/// <param name="Damage">Damage dealt: equal to <see cref="AttackValueUsed"/> on a hit, 0 on a miss.</param>
/// <param name="DefenderHpAfter">Defender's hit points after the turn, never below 0.</param>
public sealed record TurnEvent(
    int Turn,
    PlayerId AttackerId,
    PlayerId DefenderId,
    int AttackerHpBefore,
    int DefenderHpBefore,
    int AttackValueUsed,
    int DodgeChanceBasisPoints,
    int Roll,
    bool Hit,
    int Damage,
    int DefenderHpAfter);
