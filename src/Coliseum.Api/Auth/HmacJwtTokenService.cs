using System.Text;
using Coliseum.Application;
using Coliseum.Application.Options;
using Coliseum.Application.Ports;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Coliseum.Api.Auth;

/// <summary>
/// Issues HS256 JWTs. Service tokens live 24 h (SUP-09), player tokens 1 h; both carry <c>sub</c> and <c>role</c>.
/// The signing key is a secret injected through configuration, never committed.
/// </summary>
public sealed class HmacJwtTokenService(IOptions<AuthOptions> options, IClock clock) : IAuthTokenService
{
    private readonly JsonWebTokenHandler _handler = new() { SetDefaultTimesOnTokenCreation = false };
    private readonly AuthOptions _options = options.Value;

    public IssuedToken Issue(Caller caller)
    {
        var now = clock.UtcNow;
        var expires = now + (caller.IsService ? _options.ServiceTokenLifetime : _options.PlayerTokenLifetime);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expires.UtcDateTime,
            Claims = new Dictionary<string, object>
            {
                [AuthPolicies.SubjectClaim] = caller.PlayerId?.Value ?? "service",
                [AuthPolicies.RoleClaim] = caller.IsService ? AuthPolicies.RoleService : AuthPolicies.RolePlayer,
            },
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey)),
                SecurityAlgorithms.HmacSha256),
        };

        return new IssuedToken(_handler.CreateToken(descriptor), expires);
    }
}
