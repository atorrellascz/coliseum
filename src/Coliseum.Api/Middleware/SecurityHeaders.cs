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

    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app) =>
        app.Use(static (context, next) =>
        {
            context.Response.OnStarting(static state =>
            {
                var headers = ((HttpContext)state).Response.Headers;
                headers["X-Content-Type-Options"] = "nosniff";
                headers["X-Frame-Options"] = "DENY";
                headers["Referrer-Policy"] = "no-referrer";
                headers["Cache-Control"] = "no-store";
                return Task.CompletedTask;
            }, context);

            return next(context);
        });
}
