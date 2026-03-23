using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nop.Core;
using Nop.Core.Observability;
using OpenTelemetry.Exporter;
using OpenTelemetry.Extensions.Hosting;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Nop.Web.Framework.Infrastructure.Extensions;

/// <summary>
/// OpenTelemetry bootstrap for nopCommerce.
/// </summary>
public static class ObservabilityExtensions
{
    public static void ConfigureNopObservability(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var resourceBuilder = ResourceBuilder.CreateDefault()
            .AddService(
                serviceName: builder.Environment.ApplicationName,
                serviceVersion: NopVersion.FULL_VERSION,
                serviceInstanceId: Environment.MachineName);

        var otlpEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]
                           ?? builder.Configuration["OpenTelemetry:Otlp:Endpoint"];
        var exportTelemetryLogs = bool.TryParse(
            builder.Configuration["OTEL_EXPORT_LOGS"] ?? builder.Configuration["OpenTelemetry:Logs:Enabled"],
            out var enableLogs) && enableLogs;

        builder.Services
            .AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(
                serviceName: builder.Environment.ApplicationName,
                serviceVersion: NopVersion.FULL_VERSION,
                serviceInstanceId: Environment.MachineName))
            .WithTracing(tracing =>
            {
                tracing
                    .AddSource(NopTelemetry.ActivitySourceName)
                    .AddSource("Npgsql")
                    .AddSource("MySqlConnector")
                    .AddAspNetCoreInstrumentation(options =>
                    {
                        options.RecordException = true;
                        options.Filter = httpContext => !httpContext.Request.Path.StartsWithSegments("/metrics");
                    })
                    .AddHttpClientInstrumentation(options =>
                    {
                        options.RecordException = true;
                    })
                    // This repo uses linq2db/ADO.NET instead of EF Core, so DB tracing is captured
                    // with the provider instrumentation that the application actually executes.
                    .AddSqlClientInstrumentation()
                    .AddProcessor(new SensitiveActivityProcessor());

                if (!string.IsNullOrWhiteSpace(otlpEndpoint))
                {
                    tracing.AddOtlpExporter(options =>
                    {
                        options.Endpoint = new Uri(otlpEndpoint);
                        options.Protocol = OtlpExportProtocol.Grpc;
                    });
                }
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .AddMeter(NopTelemetry.MeterName)
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddPrometheusExporter();
            });

        if (string.IsNullOrWhiteSpace(otlpEndpoint) || !exportTelemetryLogs)
            return;

        builder.Logging.AddOpenTelemetry(options =>
        {
            options.SetResourceBuilder(resourceBuilder);
            options.IncludeFormattedMessage = false;
            options.IncludeScopes = false;
            options.ParseStateValues = false;
            options.AddOtlpExporter(otlpOptions =>
            {
                otlpOptions.Endpoint = new Uri(otlpEndpoint);
                otlpOptions.Protocol = OtlpExportProtocol.Grpc;
            });
        });
    }

    public static void UseNopObservability(this WebApplication application)
    {
        ArgumentNullException.ThrowIfNull(application);

        application.UseOpenTelemetryPrometheusScrapingEndpoint();
    }
}
