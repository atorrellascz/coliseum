using System.Security.Claims;
using System.Text;
using Coliseum.Application;
using Coliseum.Application.Options;
using Coliseum.Application.Ports;
using Coliseum.Domain.Players;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Coliseum.Api.Auth;

/// <summary>
/// Authentication and authorization wiring (REQ-16, ADR-08, ADR-15). Every endpoint requires a bearer token by
/// default (fallback policy); two named policies express who may do what. The token scheme is HS256 with a
/// shared secret; swapping to a corporate IdP means replacing this file and the token service, nothing else.
/// </summary>
public static class AuthPolicies
{
    public const string Service = "Service";
    public const string PlayerOrService = "PlayerOrService";

    public const string RoleClaim = "role";
    public const string SubjectClaim = "sub";
    public const string RoleService = "service";
    public const string RolePlayer = "player";

    public static IServiceCollection AddColiseumAuth(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<AuthOptions>()
            .Bind(configuration.GetSection(AuthOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<IAuthTokenService, HmacJwtTokenService>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<AuthOptions>>((jwt, auth) =>
            {
                jwt.MapInboundClaims = false; // keep "sub" and "role" as issued, no legacy SOAP claim names
                jwt.Events = new JwtBearerEvents
                {
                    // Browsers cannot set headers on WebSocket upgrades: SignalR sends the token as ?access_token=.
                    OnMessageReceived = context =>
                    {
                        string? token = context.Request.Query["access_token"];
                        if (!string.IsNullOrEmpty(token) && context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                        {
                            context.Token = token;
                        }

                        return Task.CompletedTask;
                    },
                };
                jwt.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = auth.Value.Issuer,
                    ValidateAudience = true,
                    ValidAudience = auth.Value.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(auth.Value.SigningKey)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = SubjectClaim,
                    RoleClaimType = RoleClaim,
                };
            });

        services.AddAuthorizationBuilder()
            .AddPolicy(Service, policy => policy.RequireAuthenticatedUser().RequireClaim(RoleClaim, RoleService))
            .AddPolicy(PlayerOrService, policy => policy.RequireAuthenticatedUser().RequireClaim(RoleClaim, RolePlayer, RoleService))
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

        return services;
    }

    /// <summary>Translates the validated principal into the application's <see cref="Caller"/>. The only place that reads claims.</summary>
    public static Caller ToCaller(this ClaimsPrincipal principal)
    {
        string? role = principal.FindFirstValue(RoleClaim);
        string? subject = principal.FindFirstValue(SubjectClaim);

        return role == RolePlayer && subject is not null
            ? Caller.ForPlayer(PlayerId.Unchecked(subject))
            : Caller.Service;
    }
}
