using Coliseum.Mcp.Options;
using Coliseum.Mcp.Tools;
using Coliseum.ServiceDefaults;
using Microsoft.Extensions.Options;

namespace Coliseum.Mcp;

/// <summary>
/// Composition root of the MCP server (ADR-13). Two transports:
/// Streamable HTTP on <c>/mcp</c> (remote agents, protected by <c>X-Api-Key</c>) or stdio (local clients such as
/// Claude Desktop) when the process is started with <c>--stdio</c>.
/// </summary>
public static class HostingExtensions
{
    public const string ServiceName = "coliseum-mcp";
    public const string StdioFlag = "--stdio";
    public const string HttpPath = "/mcp";

    public static bool IsStdio(string[] args) => args.Contains(StdioFlag, StringComparer.OrdinalIgnoreCase);

    public static WebApplicationBuilder AddColiseumMcp(this WebApplicationBuilder builder, bool stdio)
    {
        builder.AddServiceDefaults(ServiceName, includeRedisTracing: false);

        if (stdio)
        {
            // stdout is the protocol channel in stdio mode: logs must go to stderr only.
            builder.Logging.ClearProviders();
            builder.Logging.AddConsole(console => console.LogToStandardErrorThreshold = LogLevel.Trace);
        }

        builder.Services.AddOptions<McpOptions>()
            .Bind(builder.Configuration.GetSection(McpOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<ServiceTokenProvider>();
        builder.Services.AddHttpClient(ServiceTokenProvider.HttpClientName, ConfigureBaseAddress);
        builder.Services.AddHttpClient<ColiseumApiClient>(ConfigureBaseAddress);

        var mcp = builder.Services
            .AddMcpServer(options => options.ServerInfo = new() { Name = "coliseum", Version = "0.1.0" })
            .WithToolsFromAssembly(typeof(SimulationTools).Assembly);

        if (stdio)
        {
            mcp.WithStdioServerTransport();
        }
        else
        {
            mcp.WithHttpTransport();
        }

        return builder;
    }

    public static WebApplication UseColiseumMcp(this WebApplication app, bool stdio)
    {
        if (stdio)
        {
            return app; // no HTTP surface at all in stdio mode
        }

        app.MapDefaultEndpoints();
        app.UseMcpApiKey(HttpPath);
        app.MapMcp(HttpPath);
        return app;
    }

    private static void ConfigureBaseAddress(IServiceProvider provider, HttpClient http) =>
        http.BaseAddress = new Uri(provider.GetRequiredService<IOptions<McpOptions>>().Value.ApiBaseUrl.TrimEnd('/') + "/");
}
