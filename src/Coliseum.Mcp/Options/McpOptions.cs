using System.ComponentModel.DataAnnotations;

namespace Coliseum.Mcp.Options;

/// <summary>MCP host settings (section <c>Mcp</c>). Secrets come from the environment, never from committed files.</summary>
public sealed class McpOptions
{
    public const string SectionName = "Mcp";

    /// <summary>Base URL of the Coliseum API the tools call.</summary>
    [Required]
    public string ApiBaseUrl { get; set; } = "http://localhost:8080";

    /// <summary>API key exchanged for a service token at the Coliseum API.</summary>
    [Required]
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Key an MCP client must present in <c>X-Api-Key</c> to reach the HTTP transport ("protect all endpoints").</summary>
    [Required]
    public string ClientApiKey { get; set; } = string.Empty;
}
