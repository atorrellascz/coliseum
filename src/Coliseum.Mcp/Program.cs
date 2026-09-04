// Composition root of the MCP server (ADR-13). Two transports:
//   default  -> Streamable HTTP on /mcp (for remote agents; protected by X-Api-Key), plus /healthz and /metrics
//   --stdio  -> stdio (for local clients such as Claude Desktop or an IDE), no web server
using Coliseum.Mcp;
using Coliseum.Mcp.Options;
using Coliseum.Mcp.Tools;
using Coliseum.ServiceDefaults;
using Microsoft.Extensions.Options;

bool stdio = args.Contains("--stdio", StringComparer.OrdinalIgnoreCase);

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults("coliseum-mcp", includeRedisTracing: false);

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

var app = builder.Build();

if (stdio)
{
    await app.RunAsync();
    return;
}

app.MapDefaultEndpoints();
app.UseMcpApiKey("/mcp");
app.MapMcp("/mcp");
app.Run();

static void ConfigureBaseAddress(IServiceProvider provider, HttpClient http) =>
    http.BaseAddress = new Uri(provider.GetRequiredService<IOptions<McpOptions>>().Value.ApiBaseUrl.TrimEnd('/') + "/");
