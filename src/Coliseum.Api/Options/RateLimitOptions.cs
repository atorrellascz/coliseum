using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace Coliseum.Api.Options;

/// <summary>Fixed window per caller (section <c>RateLimit</c>): 100 requests per 10 seconds by default; 429 with Retry-After.</summary>
public sealed class RateLimitOptions
{
    public const string SectionName = "RateLimit";

    public int PermitLimit { get; set; } = 100;

    public TimeSpan Window { get; set; } = TimeSpan.FromSeconds(10);
}

public static class RateLimiting
{
    /// <summary>
    /// Partitions by bearer token when present (hashed, never stored raw), otherwise by client IP.
    /// Probes and scrapers are exempt.
    /// </summary>
    public static IServiceCollection AddColiseumRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection(RateLimitOptions.SectionName).Get<RateLimitOptions>() ?? new RateLimitOptions();

        services.AddRateLimiter(limiter =>
        {
            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            limiter.OnRejected = (context, _) =>
            {
                context.HttpContext.Response.Headers.RetryAfter = ((int)options.Window.TotalSeconds).ToString(CultureInfo.InvariantCulture);
                return ValueTask.CompletedTask;
            };

            limiter.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            {
                if (context.Request.Path.StartsWithSegments("/healthz") || context.Request.Path.StartsWithSegments("/metrics"))
                {
                    return RateLimitPartition.GetNoLimiter("infrastructure");
                }

                return RateLimitPartition.GetFixedWindowLimiter(PartitionKey(context), _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = options.PermitLimit,
                    Window = options.Window,
                    QueueLimit = 0,
                });
            });
        });

        return services;
    }

    private static string PartitionKey(HttpContext context)
    {
        string? authorization = context.Request.Headers.Authorization.FirstOrDefault();
        if (!string.IsNullOrEmpty(authorization))
        {
            return "token:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(authorization)))[..16];
        }

        return "ip:" + (context.Connection.RemoteIpAddress?.ToString() ?? "unknown");
    }
}
