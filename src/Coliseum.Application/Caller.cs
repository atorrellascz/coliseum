using Coliseum.Domain.Players;

namespace Coliseum.Application;

/// <summary>Who is invoking a use case. Hosts build it from the bearer token; use cases never look at claims.</summary>
public enum CallerRole
{
    /// <summary>Back-end / operator token obtained by exchanging an API key. May act on behalf of any player.</summary>
    Service,

    /// <summary>Token issued to one player. May only act as that player.</summary>
    Player,
}

/// <summary>
/// Identity of the caller as the application sees it. Authorization rules that depend on data
/// ("you may only attack as yourself", "you may only read your own battles") are enforced in the use cases
/// with this object; endpoint-level policies (who may create players at all) stay in the host.
/// </summary>
public sealed record Caller(CallerRole Role, PlayerId? PlayerId)
{
    public static Caller Service { get; } = new(CallerRole.Service, null);

    public static Caller ForPlayer(PlayerId playerId) => new(CallerRole.Player, playerId);

    public bool IsService => Role == CallerRole.Service;

    public bool Is(PlayerId playerId) => Role == CallerRole.Player && PlayerId == playerId;
}
