using System.Security.Cryptography;
using System.Text;
using Coliseum.Mcp.Options;
using Microsoft.Extensions.Options;

namespace Coliseum.Mcp;

/// <summary>
/// Protects the HTTP transport: every request under <c>/mcp</c> must carry the configured client key in
/// <c>X-Api-Key</c>. Constant-time comparison, 401 otherwise. Health and metrics stay open for probes.
/// </summary>
public static class McpApiKeyGuard
{
    public const string HeaderName = "X-Api-Key";

    public static IApplicationBuilder UseMcpApiKey(this IApplicationBuilder app, PathString basePath) =>
        app.UseWhen(context => context.Request.Path.StartsWithSegments(basePath), branch => branch.Use(async (context, next) =>
        {
            string expected = context.RequestServices.GetRequiredService<IOptions<McpOptions>>().Value.ClientApiKey;
            string? presented = context.Request.Headers[HeaderName].FirstOrDefault();

            if (presented is null || !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(presented)))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsync("Missing or invalid X-Api-Key.");
                return;
            }

            await next(context);
        }));
}
