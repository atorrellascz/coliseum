using System.Reflection;
using Coliseum.Application.Telemetry;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Coliseum.ServiceDefaults;

/// <summary>
/// Host plumbing shared by the API, the worker and the MCP server (OPS-01): structured JSON logs with trace ids,
/// OpenTelemetry traces + metrics + logs, Prometheus scraping, OTLP export when an endpoint is configured,
/// health checks and resilient HttpClients. Business projects never see any of this.
/// </summary>
public static class Extensions
{
    /// <summary>Environment variable understood by every host as a shortcut for <c>Redis:ConnectionString</c>.</summary>
    public const string RedisUrlVariable = "REDIS_URL";

    public static WebApplicationBuilder AddServiceDefaults(this WebApplicationBuilder builder, string serviceName, bool includeRedisTracing = true)
    {
        builder.Configuration.AddEnvironmentAliases();

        builder.Logging.ClearProviders();
        if (builder.Environment.IsDevelopment())
        {
            builder.Logging.AddSimpleConsole(options => options.SingleLine = true);
        }
        else
        {
            builder.Logging.AddJsonConsole(options =>
            {
                options.IncludeScopes = true;
                options.UseUtcTimestamp = true;
                options.TimestampFormat = "O";
            });
        }

        builder.Logging.AddOpenTelemetry(options =>
        {
            options.IncludeScopes = true;
            options.IncludeFormattedMessage = true;
        });

        string version = Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "0.0.0";

        var openTelemetry = builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(serviceName, serviceVersion: version)
                .AddAttributes([new KeyValuePair<string, object>("deployment.environment", builder.Environment.EnvironmentName)]))
            .WithTracing(tracing =>
            {
                tracing
                    .AddAspNetCoreInstrumentation(options => options.Filter = context => !IsInfrastructurePath(context.Request.Path))
                    .AddHttpClientInstrumentation()
                    .AddSource(ColiseumTelemetry.ServiceName);

                if (includeRedisTracing)
                {
                    tracing.AddRedisInstrumentation();
                }
            })
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddMeter(ColiseumTelemetry.ServiceName)
                .AddPrometheusExporter());

        // OTLP is opt-in: without an endpoint the exporter would retry localhost:4317 forever and spam the logs.
        if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
        {
            openTelemetry.UseOtlpExporter();
        }

        builder.Services.AddHealthChecks();
        builder.Services.ConfigureHttpClientDefaults(http => http.AddStandardResilienceHandler());

        return builder;
    }

    /// <summary>Maps <c>REDIS_URL</c> onto <c>Redis:ConnectionString</c> so Compose and Kubernetes can use the familiar name.</summary>
    public static IConfigurationBuilder AddEnvironmentAliases(this IConfigurationBuilder configuration)
    {
        string? redisUrl = Environment.GetEnvironmentVariable(RedisUrlVariable);
        if (!string.IsNullOrWhiteSpace(redisUrl))
        {
            configuration.AddInMemoryCollection([new KeyValuePair<string, string?>("Redis:ConnectionString", redisUrl)]);
        }

        return configuration;
    }

    private static bool IsInfrastructurePath(PathString path) =>
        path.StartsWithSegments("/healthz") || path.StartsWithSegments("/metrics");
}
