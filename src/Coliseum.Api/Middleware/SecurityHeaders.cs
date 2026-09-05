namespace Coliseum.Api.Middleware;

/// <summary>Defensive defaults: conservative response headers, a small request body limit and an explicit CORS allow-list.</summary>
public static class SecurityHeaders
{
    public const long MaxRequestBodyBytes = 64 * 1024;
    public const string CorsPolicy = "clients";

    public static WebApplicationBuilder ConfigureRequestLimits(this WebApplicationBuilder builder)
    {
        builder.WebHost.ConfigureKestrel(kestrel =>
        {
            kestrel.AddServerHeader = false;
            kestrel.Limits.MaxRequestBodySize = MaxRequestBodyBytes;
        });

        string[] origins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? [];
        builder.Services.AddCors(cors => cors.AddPolicy(CorsPolicy, policy =>
            policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod().AllowCredentials()));

        return builder;
    }

    /// <summary>
    /// Content Security Policy for the static clients (arena, back-office, widget demo, landing page): scripts only
    /// from this origin and cdnjs (SignalR, Chart.js), no inline scripts, connections only to this origin (REST +
    /// WebSocket), no framing. API responses are JSON and get no CSP; Scalar (/scalar) ships its own inline assets
    /// and is excluded on purpose.
    /// </summary>
    public const string StaticContentSecurityPolicy =
        "default-src 'self'; script-src 'self' https://cdnjs.cloudflare.com; style-src 'self' 'unsafe-inline'; " +
        "img-src 'self' data:; font-src 'self'; connect-src 'self' ws: wss:; frame-ancestors 'none'; base-uri 'self'; form-action 'self'";

    private static readonly string[] StaticPrefixes = ["/arena", "/backoffice", "/widget"];

    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app) =>
        app.Use(static (context, next) =>
        {
            context.Response.OnStarting(static state =>
            {
                var http = (HttpContext)state;
                var headers = http.Response.Headers;
                headers["X-Content-Type-Options"] = "nosniff";
                headers["X-Frame-Options"] = "DENY";
                headers["Referrer-Policy"] = "no-referrer";
                headers["Cache-Control"] = "no-store";
                if (IsStaticClient(http.Request.Path))
                {
                    headers["Content-Security-Policy"] = StaticContentSecurityPolicy;
                }

                return Task.CompletedTask;
            }, context);

            return next(context);
        });

    private static bool IsStaticClient(PathString path) =>
        path == "/" || path == "/index.html" || StaticPrefixes.Any(prefix => path.StartsWithSegments(prefix));
}
