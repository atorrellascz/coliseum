// Composition only (ARQ-01): what the MCP server is made of lives in HostingExtensions; nothing else belongs here.
using Coliseum.Mcp;

bool stdio = HostingExtensions.IsStdio(args);
var builder = WebApplication.CreateBuilder(args).AddColiseumMcp(stdio);
var app = builder.Build().UseColiseumMcp(stdio);
await app.RunAsync();
