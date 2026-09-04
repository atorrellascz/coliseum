using System.ComponentModel.DataAnnotations;

namespace Coliseum.Application.Options;

/// <summary>
/// Token settings (section <c>Auth</c>). The signing key and API keys are secrets: they come from environment
/// variables or a Kubernetes Secret, never from a committed appsettings file.
/// </summary>
public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    /// <summary>HS256 key. 32 bytes minimum, as required by the JWT spec for HMAC-SHA256.</summary>
    [Required]
    [MinLength(32)]
    public string SigningKey { get; set; } = string.Empty;

    /// <summary>Keys accepted by <c>POST /auth/token</c>. Compared in constant time.</summary>
    [Required]
    [MinLength(1)]
    public IList<string> ApiKeys { get; } = [];

    public string Issuer { get; set; } = "coliseum";

    public string Audience { get; set; } = "coliseum";

    public TimeSpan ServiceTokenLifetime { get; set; } = TimeSpan.FromHours(24);

    public TimeSpan PlayerTokenLifetime { get; set; } = TimeSpan.FromHours(1);
}
