namespace Coliseum.Application.Ports;

/// <summary>
/// Issues bearer tokens for a <see cref="Caller"/>. Validation is the host's middleware job, so this port stays
/// tiny and the whole scheme (HS256 today, a corporate IdP tomorrow, ADR-08) is a one-adapter swap.
/// </summary>
public interface IAuthTokenService
{
    IssuedToken Issue(Caller caller);
}

public sealed record IssuedToken(string AccessToken, DateTimeOffset ExpiresAt);
