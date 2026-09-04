using Coliseum.Domain.Players;

namespace Coliseum.Domain.Battles;

/// <summary>
/// Mutable per-battle state of one fighter. Lives only inside <see cref="BattleEngine.Run"/>; the immutable
/// <see cref="Player"/> is never modified. Keeping the scratch state here keeps the engine loop readable.
/// </summary>
internal sealed class Combatant
{
    public Combatant(Player player)
    {
        Id = player.Id;
        BaseAttack = player.Stats.Attack;
        Defense = player.Stats.Defense;
        MaxHitPoints = player.Stats.HitPoints;
        HitPoints = player.Stats.HitPoints;
        Resources = player.Resources;
    }

    public PlayerId Id { get; }

    public int BaseAttack { get; }

    public int Defense { get; }

    public int MaxHitPoints { get; }

    public int HitPoints { get; set; }

    public Resources Resources { get; }

    public bool IsAlive => HitPoints > 0;
}
