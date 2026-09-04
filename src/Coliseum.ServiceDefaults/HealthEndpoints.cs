using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using OpenTelemetry.Metrics;

namespace Coliseum.ServiceDefaults;

/// <summary>
/// Operational endpoints every host exposes (OPS-04): liveness (process up), readiness (dependencies answer,
/// checks tagged <c>ready</c>) and Prometheus metrics. All anonymous: probes and scrapers carry no token.
/// </summary>
public static class HealthEndpoints
{
    public const string ReadyTag = "ready";

    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        app.MapHealthChecks("/healthz/live", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();
        app.MapHealthChecks("/healthz/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains(ReadyTag) }).AllowAnonymous();
        app.MapPrometheusScrapingEndpoint("/metrics").AllowAnonymous();
        return app;
    }
}
